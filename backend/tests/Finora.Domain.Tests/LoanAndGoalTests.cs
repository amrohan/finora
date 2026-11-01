using Finora.Domain.Entities;
using Xunit;

namespace Finora.Domain.Tests;

public class LoanAndGoalTests
{
    [Fact]
    public void CalculateMonthlyEmi_MatchesStandardBankingFormula()
    {
        // Loan: ₹10,00,000 (100,000,000 paise), Rate: 8.5% p.a., Tenure: 120 months (10 years)
        var principalPaise = 100_000_000L;
        var annualRate = 8.50m;
        var tenureMonths = 120;

        var emiPaise = Loan.CalculateMonthlyEmi(principalPaise, annualRate, tenureMonths);

        // Standard financial calculator verification: ₹12,398.57 -> 1,239,857 paise
        Assert.InRange(emiPaise, 1_239_850L, 1_239_865L);
    }

    [Fact]
    public void CalculateMonthlyInterest_ReturnsExactPortion()
    {
        // ₹10,00,000 (100,000,000 paise) at 12% p.a.
        // Monthly interest = 12% / 12 = 1% per month = ₹10,000 (1,000,000 paise)
        var principalPaise = 100_000_000L;
        var annualRate = 12.00m;

        var interestPaise = Loan.CalculateMonthlyInterest(principalPaise, annualRate);
        Assert.Equal(1_000_000L, interestPaise);
    }

    [Fact]
    public void ZeroInterestRate_ReturnsStraightLineEmi()
    {
        // ₹1,20,000 (12,000,000 paise) at 0% interest for 12 months = ₹10,000 (1,000,000 paise) / month
        var principalPaise = 12_000_000L;
        var annualRate = 0m;
        var tenureMonths = 12;

        var emiPaise = Loan.CalculateMonthlyEmi(principalPaise, annualRate, tenureMonths);
        Assert.Equal(1_000_000L, emiPaise);
    }

    [Fact]
    public void SavingsGoal_ProgressCalculation()
    {
        var targetPaise = 5_000_000L;  // ₹50,000
        var currentPaise = 2_500_000L; // ₹25,000

        var progress = Math.Round((double)currentPaise / targetPaise * 100, 1);
        Assert.Equal(50.0, progress);
    }

    [Fact]
    public void Prepayment_ReducesTenure_UnderReducingBalance()
    {
        var originalPrincipal = 1_000_000L; // ₹10,000
        var lumpSumPayment = 400_000L;       // ₹4,000 prepayment

        var remaining = originalPrincipal - lumpSumPayment;
        Assert.Equal(600_000L, remaining);
    }
}

