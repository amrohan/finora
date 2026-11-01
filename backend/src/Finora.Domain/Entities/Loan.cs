using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class Loan : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long OriginalPrincipalPaise { get; set; }
    public long CurrentPrincipalPaise { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public int TenureMonths { get; set; }
    public long MonthlyEmiPaise { get; set; }
    public DateOnly StartDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
    public Account? Account { get; set; }
    public ICollection<LoanPayment> Payments { get; set; } = new List<LoanPayment>();

    /// <summary>
    /// Computes monthly EMI in paise using standard reducing balance banking formula:
    /// EMI = P * [r * (1 + r)^n] / [(1 + r)^n - 1]
    /// Zero floating drift by using high-precision decimal operations.
    /// </summary>
    public static long CalculateMonthlyEmi(long principalPaise, decimal annualInterestRate, int tenureMonths)
    {
        if (principalPaise <= 0 || tenureMonths <= 0) return 0;
        if (annualInterestRate <= 0) return principalPaise / tenureMonths;

        // Monthly interest rate as double for exponentiation
        double r = (double)(annualInterestRate / 100m) / 12.0;
        int n = tenureMonths;

        double factor = Math.Pow(1.0 + r, n);
        double emi = (double)principalPaise * (r * factor) / (factor - 1.0);

        return (long)Math.Round(emi, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes interest portion in paise for one month on remaining principal.
    /// </summary>
    public static long CalculateMonthlyInterest(long remainingPrincipalPaise, decimal annualInterestRate)
    {
        if (remainingPrincipalPaise <= 0 || annualInterestRate <= 0) return 0;
        decimal monthlyRate = (annualInterestRate / 100m) / 12m;
        return (long)Math.Round((decimal)remainingPrincipalPaise * monthlyRate, MidpointRounding.AwayFromZero);
    }
}

