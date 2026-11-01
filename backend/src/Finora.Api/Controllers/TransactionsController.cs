using Finora.Application.Common;
using Finora.Application.Features.Transactions;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public TransactionsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>
    /// Returns a paginated, filtered list of visible transactions.
    /// Excludes Private transactions owned by other members.
    /// Transfer transactions are included but flagged with type=Transfer.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] Guid? accountId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] TransactionType? type = null,
        CancellationToken cancellationToken = default)
    {
        var (userId, _) = GetContext();
        if (userId == null) return Unauthorized();

        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var query = _dbContext.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Splits).ThenInclude(s => s.Category)
            .Where(t =>
                t.Visibility == VisibilityLevel.Household ||
                t.CreatedByUserId == userId.Value ||
                _dbContext.AccessGrants.Any(ag =>
                    ag.ResourceType == "Transaction" &&
                    ag.ResourceId == t.Id &&
                    ag.GrantedToUserId == userId.Value));

        if (startDate.HasValue) query = query.Where(t => t.Date >= startDate.Value);
        if (endDate.HasValue) query = query.Where(t => t.Date <= endDate.Value);
        if (accountId.HasValue) query = query.Where(t => t.AccountId == accountId.Value);
        if (categoryId.HasValue) query = query.Where(t => t.CategoryId == categoryId.Value);
        if (type.HasValue) query = query.Where(t => t.TransactionType == type.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return Ok(new PagedResult<TransactionDto>(dtos, total, page, pageSize));
    }

    /// <summary>
    /// Creates an Expense or Income transaction.
    /// Updates account balance atomically inside a DB transaction.
    /// Validates split amounts if IsSplit.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTransaction(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.TransactionType == TransactionType.Transfer)
            return UnprocessableEntity(Problem(
                "Use POST /api/v1/transactions/transfers to record transfers.", statusCode: 422));

        if (request.AmountPaise <= 0)
            return UnprocessableEntity(Problem("Amount must be greater than zero.", statusCode: 422));

        // Validate account belongs to household
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account == null)
            return UnprocessableEntity(Problem("Account not found in this household.", statusCode: 422));

        // Validate category if provided
        if (request.CategoryId.HasValue)
        {
            var catExists = await _dbContext.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (!catExists)
                return UnprocessableEntity(Problem("Category not found in this household.", statusCode: 422));
        }

        // Validate splits
        if (request.Splits != null && request.Splits.Count > 0)
        {
            var splitSum = request.Splits.Sum(s => s.AmountPaise);
            if (splitSum != request.AmountPaise)
                return UnprocessableEntity(Problem(
                    $"Split amounts sum to {splitSum} paise but transaction amount is {request.AmountPaise} paise.",
                    statusCode: 422));

            if (request.Splits.Any(s => s.AmountPaise <= 0))
                return UnprocessableEntity(Problem("Each split amount must be greater than zero.", statusCode: 422));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = request.AccountId,
                CategoryId = request.CategoryId,
                CreatedByUserId = userId.Value,
                TransactionType = request.TransactionType,
                AmountPaise = request.AmountPaise,
                Date = request.Date,
                Payee = request.Payee,
                Notes = request.Notes,
                Visibility = request.Visibility,
                IsSplit = request.Splits != null && request.Splits.Count > 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Transactions.Add(transaction);

            if (transaction.IsSplit)
            {
                foreach (var split in request.Splits!)
                {
                    _dbContext.TransactionSplits.Add(new TransactionSplit
                    {
                        Id = Guid.NewGuid(),
                        HouseholdId = householdId.Value,
                        TransactionId = transaction.Id,
                        CategoryId = split.CategoryId,
                        AmountPaise = split.AmountPaise,
                        Notes = split.Notes
                    });
                }
            }

            // Update account balance
            if (request.TransactionType == TransactionType.Income)
                account.CurrentBalancePaise += request.AmountPaise;
            else
                account.CurrentBalancePaise -= request.AmountPaise;

            account.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value, userId.Value,
                "TransactionCreated", "Transaction", transaction.Id.ToString(),
                newValues: new { transaction.TransactionType, transaction.AmountPaise, transaction.Date });

            // Reload for response
            var created = await _dbContext.Transactions
                .Include(t => t.Account)
                .Include(t => t.Category)
                .Include(t => t.Splits).ThenInclude(s => s.Category)
                .FirstAsync(t => t.Id == transaction.Id, cancellationToken);

            return CreatedAtAction(nameof(ListTransactions), MapToDto(created));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Records a transfer between two accounts in the same household.
    /// Creates two linked Transaction records (type=Transfer) and a Transfer join entity.
    /// Transfer transactions are excluded from expense/income totals.
    /// </summary>
    [HttpPost("transfers")]
    public async Task<IActionResult> RecordTransfer(
        [FromBody] RecordTransferRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.AmountPaise <= 0)
            return UnprocessableEntity(Problem("Amount must be greater than zero.", statusCode: 422));

        if (request.SourceAccountId == request.DestinationAccountId)
            return UnprocessableEntity(Problem("Source and destination accounts must be different.", statusCode: 422));

        var sourceAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.SourceAccountId, cancellationToken);
        if (sourceAccount == null)
            return UnprocessableEntity(Problem("Source account not found in this household.", statusCode: 422));

        var destAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.DestinationAccountId, cancellationToken);
        if (destAccount == null)
            return UnprocessableEntity(Problem("Destination account not found in this household.", statusCode: 422));

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;

            // Source debit
            var sourceTxn = new Transaction
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = request.SourceAccountId,
                CategoryId = null,
                CreatedByUserId = userId.Value,
                TransactionType = TransactionType.Transfer,
                AmountPaise = request.AmountPaise,
                Date = request.Date,
                Payee = null,
                Notes = request.Notes,
                Visibility = VisibilityLevel.Household,
                IsSplit = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            // Destination credit
            var destTxn = new Transaction
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = request.DestinationAccountId,
                CategoryId = null,
                CreatedByUserId = userId.Value,
                TransactionType = TransactionType.Transfer,
                AmountPaise = request.AmountPaise,
                Date = request.Date,
                Payee = null,
                Notes = request.Notes,
                Visibility = VisibilityLevel.Household,
                IsSplit = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            var transfer = new Transfer
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                SourceTransactionId = sourceTxn.Id,
                DestinationTransactionId = destTxn.Id,
                CreatedAt = now
            };

            _dbContext.Transactions.Add(sourceTxn);
            _dbContext.Transactions.Add(destTxn);
            _dbContext.Transfers.Add(transfer);

            // Balance mutations — transfers never affect household net expense (type=Transfer excluded from analytics)
            sourceAccount.CurrentBalancePaise -= request.AmountPaise;
            destAccount.CurrentBalancePaise += request.AmountPaise;
            sourceAccount.UpdatedAt = now;
            destAccount.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value, userId.Value,
                "TransferRecorded", "Transfer", transfer.Id.ToString(),
                newValues: new
                {
                    request.SourceAccountId,
                    request.DestinationAccountId,
                    request.AmountPaise
                });

            return Ok(new
            {
                TransferId = transfer.Id,
                SourceTransactionId = sourceTxn.Id,
                DestinationTransactionId = destTxn.Id,
                request.AmountPaise,
                request.Date
            });
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Deletes a transaction and reverses its balance effect.
    /// For Transfer transactions: deletes both linked transactions and the Transfer record atomically.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid id, CancellationToken cancellationToken)
    {
        var (userId, _) = GetContext();
        if (userId == null) return Unauthorized();

        var transaction = await _dbContext.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (transaction == null) return NotFound();

        // Only creator may delete
        if (transaction.CreatedByUserId != userId.Value) return Forbid();

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == transaction.AccountId, cancellationToken);
        if (account == null) return NotFound();

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (transaction.TransactionType == TransactionType.Transfer)
            {
                // Find the Transfer join record
                var transfer = await _dbContext.Transfers
                    .FirstOrDefaultAsync(tr =>
                        tr.SourceTransactionId == id || tr.DestinationTransactionId == id,
                        cancellationToken);

                if (transfer != null)
                {
                    var linkedId = transfer.SourceTransactionId == id
                        ? transfer.DestinationTransactionId
                        : transfer.SourceTransactionId;

                    var linkedTxn = await _dbContext.Transactions
                        .FirstOrDefaultAsync(t => t.Id == linkedId, cancellationToken);

                    var linkedAccount = linkedTxn != null
                        ? await _dbContext.Accounts
                            .FirstOrDefaultAsync(a => a.Id == linkedTxn.AccountId, cancellationToken)
                        : null;

                    // Determine which is source and which is dest based on Transfer record
                    var isSource = transfer.SourceTransactionId == id;

                    // Reverse balances
                    // Source transaction deducted from source account → restore
                    if (isSource)
                    {
                        account.CurrentBalancePaise += transaction.AmountPaise;   // restore source
                        if (linkedAccount != null)
                            linkedAccount.CurrentBalancePaise -= transaction.AmountPaise; // reverse dest credit
                    }
                    else
                    {
                        account.CurrentBalancePaise -= transaction.AmountPaise;   // restore dest
                        if (linkedAccount != null)
                            linkedAccount.CurrentBalancePaise += transaction.AmountPaise; // reverse source debit
                    }

                    _dbContext.Transfers.Remove(transfer);
                    if (linkedTxn != null) _dbContext.Transactions.Remove(linkedTxn);
                }
                else
                {
                    // Orphaned transfer transaction — still reverse balance
                    account.CurrentBalancePaise += transaction.AmountPaise;
                }
            }
            else
            {
                // Reverse balance
                if (transaction.TransactionType == TransactionType.Income)
                    account.CurrentBalancePaise -= transaction.AmountPaise;
                else
                    account.CurrentBalancePaise += transaction.AmountPaise;

                if (transaction.Splits.Count > 0)
                    _dbContext.TransactionSplits.RemoveRange(transaction.Splits);
            }

            account.UpdatedAt = DateTimeOffset.UtcNow;
            _dbContext.Transactions.Remove(transaction);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                transaction.HouseholdId, userId.Value,
                "TransactionDeleted", "Transaction", transaction.Id.ToString(),
                oldValues: new { transaction.TransactionType, transaction.AmountPaise, transaction.Date });

            return NoContent();
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ------------------------------------------------------------------
    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);

    private static TransactionDto MapToDto(Transaction t) => new(
        t.Id,
        t.AccountId,
        t.Account?.Name ?? string.Empty,
        t.CategoryId,
        t.Category?.Name,
        t.TransactionType,
        t.AmountPaise,
        t.Date,
        t.Payee,
        t.Notes,
        t.Visibility,
        t.IsSplit,
        t.IsSplit && t.Splits.Count > 0
            ? t.Splits.Select(s => new TransactionSplitDto(
                s.CategoryId, s.Category?.Name, s.AmountPaise, s.Notes)).ToList()
            : null,
        t.CreatedAt);
}

