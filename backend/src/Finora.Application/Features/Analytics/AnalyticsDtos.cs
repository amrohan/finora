namespace Finora.Application.Features.Analytics;

public record NetWorthOverviewDto(
    long TotalAssetsPaise,
    long TotalLiabilitiesPaise,
    long NetWorthPaise,
    long CurrentMonthInflowPaise,
    long CurrentMonthOutflowPaise,
    double SavingsRatePercentage,
    IReadOnlyList<MonthlyCashFlowDto> CashFlowHistory);

public record MonthlyCashFlowDto(
    int Month,
    int Year,
    long InflowPaise,
    long OutflowPaise,
    long NetCashFlowPaise);

public record SpendingBreakdownDto(
    int Month,
    int Year,
    long TotalExpensePaise,
    IReadOnlyList<CategorySpendingDto> Categories,
    IReadOnlyList<MerchantSpendingDto> TopPayees);

public record CategorySpendingDto(
    Guid? CategoryId,
    string CategoryName,
    string? Icon,
    string? ColorHex,
    long AmountPaise,
    double Percentage);

public record MerchantSpendingDto(
    string Payee,
    long AmountPaise,
    int TransactionCount);

