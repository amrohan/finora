using Finora.Application.Common;
using Finora.Application.Features.Bills;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/bills")]
[Authorize]
public class BillsController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public BillsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>Lists all recurring bill schedules for the active household.</summary>
    [HttpGet]
    public async Task<IActionResult> ListBills(CancellationToken cancellationToken)
    {
        var (_, householdId) = GetContext();
        if (householdId == null) return Unauthorized();

        var bills = await _dbContext.RecurringBills
            .Include(b => b.Category)
            .OrderBy(b => b.NextDueDate)
            .Select(b => new RecurringBillDto(
                b.Id,
                b.CategoryId,
                b.Category != null ? b.Category.Name : null,
                b.Name,
                b.ExpectedAmountPaise,
                b.Frequency,
                b.NextDueDate,
                b.AutoRecord,
                b.IsActive,
                b.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(bills);
    }

    /// <summary>Creates a new recurring bill and generates upcoming occurrences for 60 days.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateBill(
        [FromBody] CreateRecurringBillRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return UnprocessableEntity(Problem("Bill name is required.", statusCode: 422));

        if (request.ExpectedAmountPaise <= 0)
            return UnprocessableEntity(Problem("Expected amount must be greater than zero.", statusCode: 422));

        if (request.CategoryId.HasValue)
        {
            var catExists = await _dbContext.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (!catExists)
                return UnprocessableEntity(Problem("Category not found in this household.", statusCode: 422));
        }

        var bill = new RecurringBill
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId.Value,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            ExpectedAmountPaise = request.ExpectedAmountPaise,
            Frequency = request.Frequency,
            NextDueDate = request.NextDueDate,
            AutoRecord = request.AutoRecord,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RecurringBills.Add(bill);

        // Generate occurrences for 60-day window
        var occurrences = bill.GenerateOccurrences(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60));
        _dbContext.BillOccurrences.AddRange(occurrences);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            householdId.Value,
            userId.Value,
            "RecurringBillCreated",
            "RecurringBill",
            bill.Id.ToString(),
            newValues: new { bill.Name, bill.ExpectedAmountPaise, bill.Frequency });

        return CreatedAtAction(nameof(ListBills), new RecurringBillDto(
            bill.Id,
            bill.CategoryId,
            null,
            bill.Name,
            bill.ExpectedAmountPaise,
            bill.Frequency,
            bill.NextDueDate,
            bill.AutoRecord,
            bill.IsActive,
            bill.CreatedAt));
    }

    /// <summary>Deletes a recurring bill and removes unhandled upcoming occurrences.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBill(Guid id, CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var bill = await _dbContext.RecurringBills
            .Include(b => b.Occurrences)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (bill == null) return NotFound();

        // Delete upcoming/unpaid occurrences
        var unpaidOccurrences = bill.Occurrences
            .Where(o => o.Status != BillOccurrenceStatus.Paid)
            .ToList();

        _dbContext.BillOccurrences.RemoveRange(unpaidOccurrences);
        _dbContext.RecurringBills.Remove(bill);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            householdId.Value,
            userId.Value,
            "RecurringBillDeleted",
            "RecurringBill",
            id.ToString());

        return NoContent();
    }

    /// <summary>
    /// Lists bill occurrences within a date window.
    /// Automatically flags past due occurrences as Overdue.
    /// </summary>
    [HttpGet("occurrences")]
    public async Task<IActionResult> ListOccurrences(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] BillOccurrenceStatus? status,
        CancellationToken cancellationToken)
    {
        var (_, householdId) = GetContext();
        if (householdId == null) return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = startDate ?? today.AddDays(-30);
        var to = endDate ?? today.AddDays(60);

        var query = _dbContext.BillOccurrences
            .Include(o => o.RecurringBill)
            .Where(o => o.DueDate >= from && o.DueDate <= to);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var occurrences = await query
            .OrderBy(o => o.DueDate)
            .ToListAsync(cancellationToken);

        // Update overdue statuses dynamically in memory / persistence
        var overdueNeedUpdate = false;
        foreach (var occ in occurrences)
        {
            if (occ.Status == BillOccurrenceStatus.Upcoming && occ.DueDate < today)
            {
                occ.Status = BillOccurrenceStatus.Overdue;
                overdueNeedUpdate = true;
            }
        }

        if (overdueNeedUpdate)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var dtos = occurrences.Select(o => new BillOccurrenceDto(
            o.Id,
            o.BillId,
            o.RecurringBill?.Name ?? "Bill",
            o.RecurringBill?.ExpectedAmountPaise ?? 0,
            o.DueDate,
            o.Status,
            o.TransactionId,
            o.PaidAt)).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Marks a bill occurrence as Paid:
    /// 1. Creates an Expense transaction linked to the chosen account.
    /// 2. Updates account balance atomically.
    /// 3. Marks occurrence Paid with timestamp and transaction ID.
    /// 4. Advances recurring bill next due date and generates future occurrences.
    /// </summary>
    [HttpPost("occurrences/{id:guid}/pay")]
    public async Task<IActionResult> PayOccurrence(
        Guid id,
        [FromBody] PayBillOccurrenceRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var occurrence = await _dbContext.BillOccurrences
            .Include(o => o.RecurringBill)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (occurrence == null) return NotFound();

        if (occurrence.Status == BillOccurrenceStatus.Paid)
            return UnprocessableEntity(Problem("This bill occurrence is already marked as paid.", statusCode: 422));

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account == null)
            return UnprocessableEntity(Problem("Account not found in this household.", statusCode: 422));

        var bill = occurrence.RecurringBill;
        if (bill == null) return NotFound("Parent bill schedule not found.");

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;

            // 1. Create expense transaction
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = request.AccountId,
                CategoryId = bill.CategoryId,
                CreatedByUserId = userId.Value,
                TransactionType = TransactionType.Expense,
                AmountPaise = bill.ExpectedAmountPaise,
                Date = request.PaymentDate,
                Payee = bill.Name,
                Notes = request.Notes ?? $"Auto-recorded bill payment for {bill.Name}",
                Visibility = VisibilityLevel.Household,
                IsSplit = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Transactions.Add(transaction);

            // 2. Decrement account balance
            account.CurrentBalancePaise -= bill.ExpectedAmountPaise;
            account.UpdatedAt = now;

            // 3. Mark occurrence Paid
            occurrence.Status = BillOccurrenceStatus.Paid;
            occurrence.TransactionId = transaction.Id;
            occurrence.PaidAt = now;

            // 4. Advance bill's next due date and maintain 60-day forward window
            bill.NextDueDate = RecurringBill.AdvanceDate(occurrence.DueDate, bill.Frequency);
            var newOccurrences = bill.GenerateOccurrences(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60));
            
            // Only add occurrences that don't already exist
            foreach (var newOcc in newOccurrences)
            {
                var exists = await _dbContext.BillOccurrences.AnyAsync(
                    o => o.BillId == bill.Id && o.DueDate == newOcc.DueDate, cancellationToken);
                if (!exists)
                {
                    _dbContext.BillOccurrences.Add(newOcc);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value,
                userId.Value,
                "BillOccurrencePaid",
                "BillOccurrence",
                occurrence.Id.ToString(),
                newValues: new { bill.Name, bill.ExpectedAmountPaise, request.PaymentDate, transactionId = transaction.Id });

            return Ok(new BillOccurrenceDto(
                occurrence.Id,
                occurrence.BillId,
                bill.Name,
                bill.ExpectedAmountPaise,
                occurrence.DueDate,
                occurrence.Status,
                occurrence.TransactionId,
                occurrence.PaidAt));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);
}
