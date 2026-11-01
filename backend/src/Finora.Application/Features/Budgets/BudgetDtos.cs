namespace Finora.Application.Features.Budgets;

public record BudgetDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string? CategoryIcon,
    string? CategoryColorHex,
    int Month,
    int Year,
    long AllocatedAmountPaise,
    long SpentAmountPaise,
    double PercentageUsed,
    bool RolloverEnabled);

public record SetBudgetRequest(
    Guid CategoryId,
    int Month,
    int Year,
    long AllocatedAmountPaise,
    bool RolloverEnabled = false);

public record BudgetSummaryDto(
    int Month,
    int Year,
    long TotalAllocatedPaise,
    long TotalSpentPaise,
    double OverallPercentageUsed,
    IReadOnlyList<BudgetDto> Budgets);

