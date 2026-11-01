using System.Globalization;
using System.Text;
using System.Text.Json;
using Finora.Application.Common;
using Finora.Application.Features.Analytics;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AnalyticsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Computes Net Worth and Cash Flow summary with multi-member privacy isolation.
    /// Private accounts/transactions owned by other members are strictly excluded.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] int months = 6,
        CancellationToken cancellationToken = default)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (months is < 1 or > 24) months = 6;

        // 1. Visible Accounts
        var visibleAccounts = await _dbContext.Accounts
            .Where(a => !a.IsArchived &&
                        (a.Visibility == VisibilityLevel.Household ||
                         a.OwnerUserId == userId.Value ||
                         _dbContext.AccessGrants.Any(ag =>
                             ag.ResourceType == "Account" &&
                             ag.ResourceId == a.Id &&
                             ag.GrantedToUserId == userId.Value)))
            .ToListAsync(cancellationToken);

        // Assets = Savings, Checking, Cash, Investment
        var assetAccounts = visibleAccounts
            .Where(a => a.Type is AccountType.Savings or AccountType.Checking or AccountType.Cash or AccountType.Investment);
        var totalAssetsPaise = assetAccounts.Sum(a => a.CurrentBalancePaise);

        // Liabilities = Credit card negative balances + Active visible loans
        var creditCardLiabilityPaise = visibleAccounts
            .Where(a => a.Type == AccountType.CreditCard && a.CurrentBalancePaise < 0)
            .Sum(a => -a.CurrentBalancePaise);

        var visibleLoans = await _dbContext.Loans
            .Include(l => l.Account)
            .Where(l => l.Account != null &&
                        (l.Account.Visibility == VisibilityLevel.Household ||
                         l.Account.OwnerUserId == userId.Value ||
                         _dbContext.AccessGrants.Any(ag =>
                             ag.ResourceType == "Account" &&
                             ag.ResourceId == l.AccountId &&
                             ag.GrantedToUserId == userId.Value)))
            .ToListAsync(cancellationToken);

        var loanLiabilityPaise = visibleLoans.Sum(l => l.CurrentPrincipalPaise);
        var totalLiabilitiesPaise = creditCardLiabilityPaise + loanLiabilityPaise;
        var netWorthPaise = totalAssetsPaise - totalLiabilitiesPaise;

        // 2. Cash Flow History
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var startMonthDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var transactions = await _dbContext.Transactions
            .Where(t => t.Date >= startMonthDate &&
                        (t.Visibility == VisibilityLevel.Household ||
                         t.CreatedByUserId == userId.Value ||
                         _dbContext.AccessGrants.Any(ag =>
                             ag.ResourceType == "Transaction" &&
                             ag.ResourceId == t.Id &&
                             ag.GrantedToUserId == userId.Value)))
            .ToListAsync(cancellationToken);

        var cashFlowHistory = new List<MonthlyCashFlowDto>();
        for (var i = months - 1; i >= 0; i--)
        {
            var date = new DateOnly(today.Year, today.Month, 1).AddMonths(-i);
            var m = date.Month;
            var y = date.Year;

            var monthTxs = transactions.Where(t => t.Date.Month == m && t.Date.Year == y).ToList();

            // Transfers are strictly excluded from Inflow and Outflow
            var inflow = monthTxs
                .Where(t => t.TransactionType == TransactionType.Income)
                .Sum(t => t.AmountPaise);

            var outflow = monthTxs
                .Where(t => t.TransactionType == TransactionType.Expense)
                .Sum(t => t.AmountPaise);

            var net = inflow - outflow;
            cashFlowHistory.Add(new MonthlyCashFlowDto(m, y, inflow, outflow, net));
        }

        var currentMonthCashFlow = cashFlowHistory.Last();
        var savingsRate = currentMonthCashFlow.InflowPaise > 0
            ? Math.Round(((double)(currentMonthCashFlow.InflowPaise - currentMonthCashFlow.OutflowPaise) / currentMonthCashFlow.InflowPaise) * 100.0, 2)
            : 0.0;

        var overview = new NetWorthOverviewDto(
            totalAssetsPaise,
            totalLiabilitiesPaise,
            netWorthPaise,
            currentMonthCashFlow.InflowPaise,
            currentMonthCashFlow.OutflowPaise,
            savingsRate,
            cashFlowHistory);

        return Ok(overview);
    }

    /// <summary>
    /// Returns spending breakdown by category and top merchants for a specified month.
    /// Excludes transfers and non-permitted private transactions.
    /// </summary>
    [HttpGet("spending-breakdown")]
    public async Task<IActionResult> GetSpendingBreakdown(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var now = DateTime.UtcNow;
        var targetMonth = month ?? now.Month;
        var targetYear = year ?? now.Year;

        var startDate = new DateOnly(targetYear, targetMonth, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var expenseTransactions = await _dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.Splits).ThenInclude(s => s.Category)
            .Where(t => t.TransactionType == TransactionType.Expense &&
                        t.Date >= startDate && t.Date <= endDate &&
                        (t.Visibility == VisibilityLevel.Household ||
                         t.CreatedByUserId == userId.Value ||
                         _dbContext.AccessGrants.Any(ag =>
                             ag.ResourceType == "Transaction" &&
                             ag.ResourceId == t.Id &&
                             ag.GrantedToUserId == userId.Value)))
            .ToListAsync(cancellationToken);

        long totalExpensePaise = 0;
        var categoryMap = new Dictionary<Guid, (Guid? Id, string Name, string? Icon, string? Color, long Amount)>();
        var payeeMap = new Dictionary<string, (long Amount, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in expenseTransactions)
        {
            totalExpensePaise += t.AmountPaise;

            // Payee tally
            var payeeName = string.IsNullOrWhiteSpace(t.Payee) ? "Unspecified Payee" : t.Payee.Trim();
            if (payeeMap.TryGetValue(payeeName, out var pVal))
            {
                payeeMap[payeeName] = (pVal.Amount + t.AmountPaise, pVal.Count + 1);
            }
            else
            {
                payeeMap[payeeName] = (t.AmountPaise, 1);
            }

            // Category tally (splits vs non-split)
            if (t.IsSplit && t.Splits.Count > 0)
            {
                foreach (var split in t.Splits)
                {
                    var catId = split.CategoryId;
                    var key = catId;
                    var catName = split.Category?.Name ?? "Uncategorized";
                    var icon = split.Category?.Icon;
                    var color = split.Category?.ColorHex;

                    if (categoryMap.TryGetValue(key, out var existing))
                    {
                        categoryMap[key] = (catId, existing.Name, existing.Icon ?? icon, existing.Color ?? color, existing.Amount + split.AmountPaise);
                    }
                    else
                    {
                        categoryMap[key] = (catId, catName, icon, color, split.AmountPaise);
                    }
                }
            }
            else
            {
                var catId = t.CategoryId;
                var key = catId ?? Guid.Empty;
                var catName = t.Category?.Name ?? "Uncategorized";
                var icon = t.Category?.Icon;
                var color = t.Category?.ColorHex;

                if (categoryMap.TryGetValue(key, out var existing))
                {
                    categoryMap[key] = (catId, existing.Name, existing.Icon ?? icon, existing.Color ?? color, existing.Amount + t.AmountPaise);
                }
                else
                {
                    categoryMap[key] = (catId, catName, icon, color, t.AmountPaise);
                }
            }
        }

        var categories = categoryMap.Values
            .Select(c => new CategorySpendingDto(
                c.Id,
                c.Name,
                c.Icon,
                c.Color,
                c.Amount,
                totalExpensePaise > 0 ? Math.Round(((double)c.Amount / totalExpensePaise) * 100.0, 2) : 0.0))
            .OrderByDescending(c => c.AmountPaise)
            .ToList();

        var topPayees = payeeMap
            .Select(kvp => new MerchantSpendingDto(kvp.Key, kvp.Value.Amount, kvp.Value.Count))
            .OrderByDescending(p => p.AmountPaise)
            .Take(10)
            .ToList();

        return Ok(new SpendingBreakdownDto(
            targetMonth,
            targetYear,
            totalExpensePaise,
            categories,
            topPayees));
    }

    /// <summary>
    /// Exports visible transactions in CSV or JSON format.
    /// Excludes other members' private records.
    /// </summary>
    [HttpGet("export/transactions")]
    public async Task<IActionResult> ExportTransactions(
        [FromQuery] string format = "csv",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] Guid? accountId = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var query = _dbContext.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t =>
                (t.Visibility == VisibilityLevel.Household ||
                 t.CreatedByUserId == userId.Value ||
                 _dbContext.AccessGrants.Any(ag =>
                     ag.ResourceType == "Transaction" &&
                     ag.ResourceId == t.Id &&
                     ag.GrantedToUserId == userId.Value)));

        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date <= endDate.Value);

        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId.Value);

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId.Value);

        var list = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var exportObjects = list.Select(t => new
            {
                Id = t.Id,
                Date = t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Type = t.TransactionType.ToString(),
                Payee = t.Payee ?? string.Empty,
                Category = t.Category?.Name ?? "Uncategorized",
                Account = t.Account?.Name ?? "Unknown",
                AmountRupees = Math.Round(t.AmountPaise / 100.0, 2),
                AmountPaise = t.AmountPaise,
                Notes = t.Notes ?? string.Empty,
                Visibility = t.Visibility.ToString()
            });

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(exportObjects, new JsonSerializerOptions { WriteIndented = true });
            return File(jsonBytes, "application/json", $"transactions_{timestamp}.json");
        }

        // CSV export
        var sb = new StringBuilder();
        sb.AppendLine("Date,Type,Payee,Category,Account,Amount (INR),Notes,Visibility");

        foreach (var t in list)
        {
            var rupees = (t.AmountPaise / 100.0).ToString("F2", CultureInfo.InvariantCulture);
            sb.Append(EscapeCsv(t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(t.TransactionType.ToString())).Append(',');
            sb.Append(EscapeCsv(t.Payee ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(t.Category?.Name ?? "Uncategorized")).Append(',');
            sb.Append(EscapeCsv(t.Account?.Name ?? "Unknown")).Append(',');
            sb.Append(rupees).Append(',');
            sb.Append(EscapeCsv(t.Notes ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(t.Visibility.ToString())).AppendLine();
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(csvBytes, "text/csv; charset=utf-8", $"transactions_{timestamp}.csv");
    }

    /// <summary>
    /// Exports visible accounts and balances in CSV or JSON format.
    /// Excludes other members' private accounts.
    /// </summary>
    [HttpGet("export/accounts")]
    public async Task<IActionResult> ExportAccounts(
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var accounts = await _dbContext.Accounts
            .Where(a => !a.IsArchived &&
                        (a.Visibility == VisibilityLevel.Household ||
                         a.OwnerUserId == userId.Value ||
                         _dbContext.AccessGrants.Any(ag =>
                             ag.ResourceType == "Account" &&
                             ag.ResourceId == a.Id &&
                             ag.GrantedToUserId == userId.Value)))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var ownerUserIds = accounts.Select(a => a.OwnerUserId).Distinct().ToList();
        var userNames = await _dbContext.Users
            .Where(u => ownerUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var exportObjects = accounts.Select(a => new
            {
                Id = a.Id,
                Name = a.Name,
                Type = a.Type.ToString(),
                BalanceRupees = Math.Round(a.CurrentBalancePaise / 100.0, 2),
                BalancePaise = a.CurrentBalancePaise,
                Currency = a.Currency,
                Visibility = a.Visibility.ToString(),
                Owner = userNames.TryGetValue(a.OwnerUserId, out var name) ? name : "Unknown"
            });

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(exportObjects, new JsonSerializerOptions { WriteIndented = true });
            return File(jsonBytes, "application/json", $"accounts_{timestamp}.json");
        }

        // CSV export
        var sb = new StringBuilder();
        sb.AppendLine("Name,Type,Balance (INR),Currency,Visibility,Owner");

        foreach (var a in accounts)
        {
            var rupees = (a.CurrentBalancePaise / 100.0).ToString("F2", CultureInfo.InvariantCulture);
            var owner = userNames.TryGetValue(a.OwnerUserId, out var name) ? name : "Unknown";
            sb.Append(EscapeCsv(a.Name)).Append(',');
            sb.Append(EscapeCsv(a.Type.ToString())).Append(',');
            sb.Append(rupees).Append(',');
            sb.Append(EscapeCsv(a.Currency)).Append(',');
            sb.Append(EscapeCsv(a.Visibility.ToString())).Append(',');
            sb.Append(EscapeCsv(owner)).AppendLine();
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(csvBytes, "text/csv; charset=utf-8", $"accounts_{timestamp}.csv");
    }

    // ------------------------------------------------------------------
    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
