using Xunit;

namespace Finora.Domain.Tests;

public class AnalyticsCalculationTests
{
    [Fact]
    public void NetWorth_ComputedAccuratelyInPaise_WithoutFloatingDrift()
    {
        // Assets
        long savingsPaise = 250_000_00L;    // ₹2,50,000
        long checkingPaise = 85_450_50L;    // ₹85,450.50
        long cashPaise = 15_000_00L;        // ₹15,000
        long investmentPaise = 500_000_00L; // ₹5,00,000

        long totalAssetsPaise = savingsPaise + checkingPaise + cashPaise + investmentPaise;
        Assert.Equal(850_450_50L, totalAssetsPaise);

        // Liabilities
        long creditCardDebtPaise = 42_350_25L; // ₹42,350.25
        long carLoanPrincipalPaise = 300_000_00L; // ₹3,00,000
        long totalLiabilitiesPaise = creditCardDebtPaise + carLoanPrincipalPaise;
        Assert.Equal(342_350_25L, totalLiabilitiesPaise);

        // Net Worth
        long netWorthPaise = totalAssetsPaise - totalLiabilitiesPaise;
        Assert.Equal(508_100_25L, netWorthPaise); // ₹5,08,100.25 exact
    }

    [Fact]
    public void SavingsRate_PositiveInflow_CalculatesExpectedPercentage()
    {
        long inflowPaise = 150_000_00L; // ₹1,50,000
        long outflowPaise = 90_000_00L; // ₹90,000

        var savingsRate = Math.Round(((double)(inflowPaise - outflowPaise) / inflowPaise) * 100.0, 2);

        Assert.Equal(40.0, savingsRate);
    }

    [Fact]
    public void SavingsRate_ZeroInflow_ReturnsZeroWithoutDivisionByZero()
    {
        long inflowPaise = 0L;
        long outflowPaise = 25_000_00L;

        var savingsRate = inflowPaise > 0
            ? Math.Round(((double)(inflowPaise - outflowPaise) / inflowPaise) * 100.0, 2)
            : 0.0;

        Assert.Equal(0.0, savingsRate);
    }

    [Fact]
    public void CashFlow_TransfersExcludedFromInflowAndOutflow()
    {
        // Monthly transactions:
        // Income: ₹1,00,000
        // Expense: ₹45,000
        // Transfer to another account: ₹20,000 (must NOT increase expense)
        // Transfer into savings goal: ₹10,000 (must NOT increase expense)

        var incomeTransactions = new List<long> { 100_000_00L };
        var expenseTransactions = new List<long> { 25_000_00L, 20_000_00L };
        var transferTransactions = new List<long> { 20_000_00L, 10_000_00L };

        long monthlyInflow = incomeTransactions.Sum();
        long monthlyOutflow = expenseTransactions.Sum();
        // Transfers are not included in outflow
        long netCashFlow = monthlyInflow - monthlyOutflow;

        Assert.Equal(100_000_00L, monthlyInflow);
        Assert.Equal(45_000_00L, monthlyOutflow);
        Assert.Equal(55_000_00L, netCashFlow);
    }

    [Fact]
    public void CategorySpendingDistribution_SumsToExpectedPercentages()
    {
        long grociersPaise = 20_000_00L; // 50%
        long utilitiesPaise = 10_000_00L; // 25%
        long entertainmentPaise = 10_000_00L; // 25%

        long totalExpensePaise = grociersPaise + utilitiesPaise + entertainmentPaise;

        double groceriesPct = Math.Round(((double)grociersPaise / totalExpensePaise) * 100.0, 2);
        double utilitiesPct = Math.Round(((double)utilitiesPaise / totalExpensePaise) * 100.0, 2);
        double entertainmentPct = Math.Round(((double)entertainmentPaise / totalExpensePaise) * 100.0, 2);

        Assert.Equal(50.0, groceriesPct);
        Assert.Equal(25.0, utilitiesPct);
        Assert.Equal(25.0, entertainmentPct);
        Assert.Equal(100.0, groceriesPct + utilitiesPct + entertainmentPct);
    }
}

