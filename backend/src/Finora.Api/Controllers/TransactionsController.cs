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
    private readonly ITransactionImportService _importService;

    public TransactionsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        ITransactionImportService importService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _importService = importService;
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
        [FromQuery] string? search = null,
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                (t.Payee != null && t.Payee.ToLower().Contains(term)) ||
                (t.Notes != null && t.Notes.ToLower().Contains(term)) ||
                (t.Category != null && t.Category.Name.ToLower().Contains(term)) ||
                (t.Account != null && t.Account.Name.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return Ok(new PagedResult<TransactionDto>(dtos, page, pageSize, total));
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

    /// <summary>
    /// Parses an uploaded Excel (.xlsx) or CSV file and returns the detected columns,
    /// a preview of rows, and all extracted data rows for client-side column mapping.
    /// </summary>
    [HttpPost("import/parse")]
    public async Task<IActionResult> ParseImportFile(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(Problem("Please select an Excel (.xlsx) or CSV file to upload.", statusCode: 400));

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(Problem("File size exceeds 10 MB limit.", statusCode: 400));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".xlsx" or ".csv" or ".tsv" or ".txt"))
            return BadRequest(Problem("Only .xlsx, .csv, and .tsv formats are supported.", statusCode: 400));

        await using var stream = file.OpenReadStream();
        var parsed = await _importService.ParseFileAsync(stream, file.FileName, cancellationToken);
        return Ok(parsed);
    }

    /// <summary>
    /// Executes atomic batch import of mapped transactions.
    /// Updates account balances atomically, applies privacy visibility,
    /// links verified categories, and skips duplicates if requested.
    /// </summary>
    [HttpPost("import/execute")]
    public async Task<IActionResult> ExecuteImport(
        [FromBody] ExecuteImportRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.Transactions == null || request.Transactions.Count == 0)
            return BadRequest(Problem("No transactions provided to import.", statusCode: 400));

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account == null)
            return UnprocessableEntity(Problem("Target account not found in this household.", statusCode: 422));

        var validCategoryIds = await _dbContext.Categories
            .Select(c => c.Id)
            .ToHashSetAsync(cancellationToken);

        var existingTxns = new HashSet<string>();
        if (request.SkipDuplicates)
        {
            var dates = request.Transactions.Select(t => t.Date).ToList();
            var minDate = dates.Min();
            var maxDate = dates.Max();

            var existingInDb = await _dbContext.Transactions
                .Where(t => t.AccountId == request.AccountId && t.Date >= minDate && t.Date <= maxDate)
                .Select(t => new { t.Date, t.AmountPaise, t.TransactionType, Payee = t.Payee ?? "" })
                .ToListAsync(cancellationToken);

            foreach (var e in existingInDb)
            {
                existingTxns.Add($"{e.Date:yyyy-MM-dd}|{e.AmountPaise}|{(int)e.TransactionType}|{e.Payee.Trim().ToLowerInvariant()}");
            }
        }

        var transactionsToAdd = new List<Transaction>();
        var skippedCount = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var item in request.Transactions)
        {
            if (item.AmountPaise <= 0 || item.TransactionType == TransactionType.Transfer)
            {
                skippedCount++;
                continue;
            }

            var payeeClean = item.Payee?.Trim() ?? string.Empty;
            var dedupeKey = $"{item.Date:yyyy-MM-dd}|{item.AmountPaise}|{(int)item.TransactionType}|{payeeClean.ToLowerInvariant()}";

            if (request.SkipDuplicates && (existingTxns.Contains(dedupeKey) || transactionsToAdd.Any(t =>
                t.Date == item.Date &&
                t.AmountPaise == item.AmountPaise &&
                t.TransactionType == item.TransactionType &&
                string.Equals(t.Payee ?? "", payeeClean, StringComparison.OrdinalIgnoreCase))))
            {
                skippedCount++;
                continue;
            }

            Guid? verifiedCategoryId = null;
            if (item.CategoryId.HasValue && validCategoryIds.Contains(item.CategoryId.Value))
            {
                verifiedCategoryId = item.CategoryId.Value;
            }

            var txn = new Transaction
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = request.AccountId,
                CategoryId = verifiedCategoryId,
                CreatedByUserId = userId.Value,
                TransactionType = item.TransactionType,
                AmountPaise = item.AmountPaise,
                Date = item.Date,
                Payee = string.IsNullOrWhiteSpace(item.Payee) ? null : item.Payee.Trim(),
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(),
                Visibility = item.Visibility,
                IsSplit = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            transactionsToAdd.Add(txn);
        }

        if (transactionsToAdd.Count == 0)
        {
            return Ok(new ExecuteImportResponse(0, skippedCount, account.CurrentBalancePaise));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Transactions.AddRange(transactionsToAdd);

            foreach (var txn in transactionsToAdd)
            {
                if (txn.TransactionType == TransactionType.Income)
                    account.CurrentBalancePaise += txn.AmountPaise;
                else if (txn.TransactionType == TransactionType.Expense)
                    account.CurrentBalancePaise -= txn.AmountPaise;
            }

            account.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value, userId.Value,
                "TransactionsImported", "Account", account.Id.ToString(),
                newValues: new { Count = transactionsToAdd.Count, Skipped = skippedCount, AccountName = account.Name });

            return Ok(new ExecuteImportResponse(transactionsToAdd.Count, skippedCount, account.CurrentBalancePaise));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Assigns or unassigns category to multiple transactions in bulk.
    /// </summary>
    [HttpPost("bulk-categorize")]
    public async Task<IActionResult> BulkCategorize(
        [FromBody] BulkCategorizeRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.TransactionIds == null || request.TransactionIds.Count == 0)
            return BadRequest(Problem("No transactions specified.", statusCode: 400));

        if (request.CategoryId.HasValue)
        {
            var catExists = await _dbContext.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (!catExists)
                return UnprocessableEntity(Problem("Category not found in this household.", statusCode: 422));
        }

        var transactions = await _dbContext.Transactions
            .Where(t => request.TransactionIds.Contains(t.Id) &&
                        (t.Visibility == VisibilityLevel.Household || t.CreatedByUserId == userId.Value))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var t in transactions)
        {
            t.CategoryId = request.CategoryId;
            t.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            householdId.Value, userId.Value,
            "BulkCategorized", "Transaction", $"{transactions.Count} transactions",
            newValues: new { request.CategoryId, Count = transactions.Count });

        return Ok(new BulkActionResultResponse(transactions.Count));
    }

    /// <summary>
    /// Deletes multiple transactions in bulk and atomically reverses account balances.
    /// </summary>
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete(
        [FromBody] BulkDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.TransactionIds == null || request.TransactionIds.Count == 0)
            return BadRequest(Problem("No transactions specified.", statusCode: 400));

        var transactions = await _dbContext.Transactions
            .Include(t => t.Account)
            .Include(t => t.Splits)
            .Where(t => request.TransactionIds.Contains(t.Id) &&
                        (t.Visibility == VisibilityLevel.Household || t.CreatedByUserId == userId.Value))
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
            return Ok(new BulkActionResultResponse(0));

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var t in transactions)
            {
                if (t.Account != null)
                {
                    if (t.TransactionType == TransactionType.Income)
                        t.Account.CurrentBalancePaise -= t.AmountPaise;
                    else if (t.TransactionType == TransactionType.Expense)
                        t.Account.CurrentBalancePaise += t.AmountPaise;

                    t.Account.UpdatedAt = DateTimeOffset.UtcNow;
                }

                if (t.Splits.Count > 0)
                {
                    _dbContext.TransactionSplits.RemoveRange(t.Splits);
                }
            }

            _dbContext.Transactions.RemoveRange(transactions);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value, userId.Value,
                "BulkDeleted", "Transaction", $"{transactions.Count} transactions",
                newValues: new { Count = transactions.Count });

            return Ok(new BulkActionResultResponse(transactions.Count));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Updates visibility (Household vs Private) for multiple transactions in bulk.
    /// </summary>
    [HttpPost("bulk-visibility")]
    public async Task<IActionResult> BulkVisibility(
        [FromBody] BulkVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.TransactionIds == null || request.TransactionIds.Count == 0)
            return BadRequest(Problem("No transactions specified.", statusCode: 400));

        var transactions = await _dbContext.Transactions
            .Where(t => request.TransactionIds.Contains(t.Id) &&
                        (t.Visibility == VisibilityLevel.Household || t.CreatedByUserId == userId.Value))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var t in transactions)
        {
            t.Visibility = request.Visibility;
            t.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            householdId.Value, userId.Value,
            "BulkVisibilityUpdated", "Transaction", $"{transactions.Count} transactions",
            newValues: new { Visibility = request.Visibility.ToString(), Count = transactions.Count });

        return Ok(new BulkActionResultResponse(transactions.Count));
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

