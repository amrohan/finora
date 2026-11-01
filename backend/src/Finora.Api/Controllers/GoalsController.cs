using Finora.Application.Common;
using Finora.Application.Features.Goals;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/goals")]
[Authorize]
public class GoalsController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public GoalsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>Lists visible savings goals for the active household.</summary>
    [HttpGet]
    public async Task<IActionResult> ListGoals(CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var goals = await _dbContext.SavingsGoals
            .OrderBy(g => g.IsCompleted)
            .ThenBy(g => g.TargetDate)
            .Select(g => new SavingsGoalDto(
                g.Id,
                g.Name,
                g.TargetAmountPaise,
                g.CurrentAmountPaise,
                g.TargetAmountPaise > 0
                    ? Math.Round((double)g.CurrentAmountPaise / g.TargetAmountPaise * 100, 1)
                    : 0.0,
                g.TargetDate,
                g.IsCompleted,
                g.Visibility,
                g.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(goals);
    }

    /// <summary>Creates a new savings goal with optional initial contribution.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateGoal(
        [FromBody] CreateSavingsGoalRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return UnprocessableEntity(Problem("Goal name is required.", statusCode: 422));

        if (request.TargetAmountPaise <= 0)
            return UnprocessableEntity(Problem("Target amount must be greater than zero.", statusCode: 422));

        var goal = new SavingsGoal
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId.Value,
            Name = request.Name.Trim(),
            TargetAmountPaise = request.TargetAmountPaise,
            CurrentAmountPaise = 0,
            TargetDate = request.TargetDate,
            IsCompleted = false,
            Visibility = request.Visibility,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.SavingsGoals.Add(goal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Process initial contribution if specified
        if (request.InitialContributionPaise > 0 && request.SourceAccountId.HasValue)
        {
            var sourceAcc = await _dbContext.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.SourceAccountId.Value, cancellationToken);

            if (sourceAcc != null)
            {
                var now = DateTimeOffset.UtcNow;
                var txn = new Transaction
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = householdId.Value,
                    AccountId = sourceAcc.Id,
                    CreatedByUserId = userId.Value,
                    TransactionType = TransactionType.Transfer, // Allocations are not expenses!
                    AmountPaise = request.InitialContributionPaise,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow),
                    Payee = $"Initial Allocation: {goal.Name}",
                    Visibility = goal.Visibility,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                sourceAcc.CurrentBalancePaise -= request.InitialContributionPaise;
                goal.CurrentAmountPaise += request.InitialContributionPaise;
                if (goal.CurrentAmountPaise >= goal.TargetAmountPaise)
                {
                    goal.IsCompleted = true;
                }

                _dbContext.Transactions.Add(txn);
                _dbContext.GoalContributions.Add(new GoalContribution
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = householdId.Value,
                    GoalId = goal.Id,
                    TransactionId = txn.Id,
                    AmountPaise = request.InitialContributionPaise,
                    ContributionDate = DateOnly.FromDateTime(DateTime.UtcNow)
                });

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await _auditService.LogAsync(
            householdId.Value,
            userId.Value,
            "SavingsGoalCreated",
            "SavingsGoal",
            goal.Id.ToString(),
            newValues: new { goal.Name, goal.TargetAmountPaise });

        var progress = goal.TargetAmountPaise > 0
            ? Math.Round((double)goal.CurrentAmountPaise / goal.TargetAmountPaise * 100, 1)
            : 0.0;

        return CreatedAtAction(nameof(ListGoals), new SavingsGoalDto(
            goal.Id,
            goal.Name,
            goal.TargetAmountPaise,
            goal.CurrentAmountPaise,
            progress,
            goal.TargetDate,
            goal.IsCompleted,
            goal.Visibility,
            goal.CreatedAt));
    }

    /// <summary>
    /// Contributes funds from an account to a savings goal.
    /// Invariant: Goal contributions are allocations of capital, NOT expenses.
    /// TransactionType is set to Transfer to keep monthly expense totals unaltered.
    /// </summary>
    [HttpPost("{id:guid}/contributions")]
    public async Task<IActionResult> Contribute(
        Guid id,
        [FromBody] ContributeToGoalRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.AmountPaise <= 0)
            return UnprocessableEntity(Problem("Contribution amount must be greater than zero.", statusCode: 422));

        var goal = await _dbContext.SavingsGoals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (goal == null) return NotFound();

        var sourceAcc = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.SourceAccountId, cancellationToken);
        if (sourceAcc == null)
            return UnprocessableEntity(Problem("Source account not found in this household.", statusCode: 422));

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;

            // 1. Create transaction as Transfer (allocation of capital, not expense!)
            var txn = new Transaction
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = sourceAcc.Id,
                CreatedByUserId = userId.Value,
                TransactionType = TransactionType.Transfer,
                AmountPaise = request.AmountPaise,
                Date = request.ContributionDate,
                Payee = $"Savings Contribution: {goal.Name}",
                Notes = request.Notes,
                Visibility = goal.Visibility,
                CreatedAt = now,
                UpdatedAt = now
            };

            // 2. Decrement source account balance
            sourceAcc.CurrentBalancePaise -= request.AmountPaise;
            sourceAcc.UpdatedAt = now;

            // 3. Increment goal balance
            goal.CurrentAmountPaise += request.AmountPaise;
            if (goal.CurrentAmountPaise >= goal.TargetAmountPaise)
            {
                goal.IsCompleted = true;
            }

            // 4. Record contribution entry
            var contribution = new GoalContribution
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                GoalId = goal.Id,
                TransactionId = txn.Id,
                AmountPaise = request.AmountPaise,
                ContributionDate = request.ContributionDate
            };

            _dbContext.Transactions.Add(txn);
            _dbContext.GoalContributions.Add(contribution);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value,
                userId.Value,
                "GoalContributionRecorded",
                "SavingsGoal",
                goal.Id.ToString(),
                newValues: new { goal.Name, request.AmountPaise, goal.CurrentAmountPaise, goal.IsCompleted });

            return Ok(new GoalContributionDto(
                contribution.Id,
                contribution.GoalId,
                contribution.AmountPaise,
                contribution.ContributionDate,
                contribution.TransactionId));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Deletes a savings goal.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGoal(Guid id, CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var goal = await _dbContext.SavingsGoals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (goal == null) return NotFound();

        _dbContext.SavingsGoals.Remove(goal);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(householdId.Value, userId.Value, "GoalDeleted", "SavingsGoal", id.ToString());

        return NoContent();
    }

    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);
}

