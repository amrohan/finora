using Finora.Application.Common;
using Finora.Application.Features.Budgets;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/budgets")]
[Authorize]
public class BudgetsController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public BudgetsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>
    /// Returns monthly budget summary with actual expenses calculated per category.
    /// Excludes other members' private transactions from spending totals.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMonthlyBudgets(
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var targetMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
        var targetYear = year is >= 2020 ? year.Value : today.Year;

        var startDate = new DateOnly(targetYear, targetMonth, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Fetch configured budgets for target month
        var budgets = await _dbContext.Budgets
            .Include(b => b.Category)
            .Where(b => b.Year == targetYear && b.Month == targetMonth)
            .OrderBy(b => b.Category!.Name)
            .ToListAsync(cancellationToken);

        // Fetch actual expenses for the month respecting record visibility
        var transactions = await _dbContext.Transactions
            .Include(t => t.Splits)
            .Where(t => t.TransactionType == TransactionType.Expense &&
                        t.Date >= startDate && t.Date <= endDate &&
                        (t.Visibility == VisibilityLevel.Household ||
                         t.CreatedByUserId == userId.Value ||
                         _dbContext.AccessGrants.Any(ag =>
                             ag.ResourceType == "Transaction" &&
                             ag.ResourceId == t.Id &&
                             ag.GrantedToUserId == userId.Value)))
            .ToListAsync(cancellationToken);

        // Aggregate spending by category (handling split transactions properly)
        var spendingMap = new Dictionary<Guid, long>();
        foreach (var txn in transactions)
        {
            if (txn.IsSplit && txn.Splits.Count > 0)
            {
                foreach (var split in txn.Splits)
                {
                    spendingMap[split.CategoryId] = spendingMap.GetValueOrDefault(split.CategoryId) + split.AmountPaise;
                }
            }
            else if (txn.CategoryId.HasValue)
            {
                spendingMap[txn.CategoryId.Value] = spendingMap.GetValueOrDefault(txn.CategoryId.Value) + txn.AmountPaise;
            }
        }

        var budgetDtos = budgets.Select(b =>
        {
            var spent = spendingMap.GetValueOrDefault(b.CategoryId, 0);
            var pct = b.AllocatedAmountPaise > 0
                ? Math.Round((double)spent / b.AllocatedAmountPaise * 100, 1)
                : 0.0;

            return new BudgetDto(
                b.Id,
                b.CategoryId,
                b.Category?.Name ?? "Uncategorized",
                b.Category?.Icon,
                b.Category?.ColorHex,
                b.Month,
                b.Year,
                b.AllocatedAmountPaise,
                spent,
                pct,
                b.RolloverEnabled);
        }).ToList();

        var totalAllocated = budgetDtos.Sum(b => b.AllocatedAmountPaise);
        var totalSpent = budgetDtos.Sum(b => b.SpentAmountPaise);
        var overallPct = totalAllocated > 0
            ? Math.Round((double)totalSpent / totalAllocated * 100, 1)
            : 0.0;

        return Ok(new BudgetSummaryDto(
            targetMonth,
            targetYear,
            totalAllocated,
            totalSpent,
            overallPct,
            budgetDtos));
    }

    /// <summary>Sets or updates a category budget allocation for a specific month.</summary>
    [HttpPost]
    public async Task<IActionResult> SetBudget(
        [FromBody] SetBudgetRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.Month is < 1 or > 12)
            return UnprocessableEntity(Problem("Month must be between 1 and 12.", statusCode: 422));

        if (request.Year < 2020)
            return UnprocessableEntity(Problem("Year must be 2020 or later.", statusCode: 422));

        if (request.AllocatedAmountPaise < 0)
            return UnprocessableEntity(Problem("Allocated amount cannot be negative.", statusCode: 422));

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category == null)
            return UnprocessableEntity(Problem("Category not found in this household.", statusCode: 422));

        var existing = await _dbContext.Budgets
            .FirstOrDefaultAsync(b =>
                b.CategoryId == request.CategoryId &&
                b.Year == request.Year &&
                b.Month == request.Month, cancellationToken);

        if (existing != null)
        {
            existing.AllocatedAmountPaise = request.AllocatedAmountPaise;
            existing.RolloverEnabled = request.RolloverEnabled;
        }
        else
        {
            existing = new Budget
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                CategoryId = request.CategoryId,
                Month = request.Month,
                Year = request.Year,
                AllocatedAmountPaise = request.AllocatedAmountPaise,
                RolloverEnabled = request.RolloverEnabled,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Budgets.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            householdId.Value,
            userId.Value,
            "BudgetConfigured",
            "Budget",
            existing.Id.ToString(),
            newValues: new { existing.CategoryId, existing.Month, existing.Year, existing.AllocatedAmountPaise });

        return Ok(new BudgetDto(
            existing.Id,
            existing.CategoryId,
            category.Name,
            category.Icon,
            category.ColorHex,
            existing.Month,
            existing.Year,
            existing.AllocatedAmountPaise,
            0,
            0.0,
            existing.RolloverEnabled));
    }

    /// <summary>Deletes a budget allocation.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBudget(Guid id, CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var budget = await _dbContext.Budgets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (budget == null) return NotFound();

        _dbContext.Budgets.Remove(budget);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            householdId.Value,
            userId.Value,
            "BudgetDeleted",
            "Budget",
            id.ToString());

        return NoContent();
    }

    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);
}

