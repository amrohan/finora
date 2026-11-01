namespace Finora.Application.Features.Loans;

public record LoanDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Name,
    long OriginalPrincipalPaise,
    long CurrentPrincipalPaise,
    decimal AnnualInterestRate,
    int TenureMonths,
    long MonthlyEmiPaise,
    DateOnly StartDate,
    DateTimeOffset CreatedAt);

public record CreateLoanRequest(
    string Name,
    long OriginalPrincipalPaise,
    decimal AnnualInterestRate,
    int TenureMonths,
    DateOnly StartDate,
    Guid? AccountId = null);

public record RecordLoanPaymentRequest(
    Guid PaymentAccountId,
    long PaymentAmountPaise,
    DateOnly PaymentDate,
    long? CustomPrincipalPaise = null,
    long? CustomInterestPaise = null,
    string? Notes = null);

public record AmortizationItemDto(
    int MonthIndex,
    DateOnly PaymentDate,
    long EmiPaise,
    long PrincipalPaise,
    long InterestPaise,
    long RemainingPrincipalPaise);

public record LoanPaymentDto(
    Guid Id,
    Guid LoanId,
    Guid TransactionId,
    long PrincipalPaise,
    long InterestPaise,
    DateOnly PaymentDate);

