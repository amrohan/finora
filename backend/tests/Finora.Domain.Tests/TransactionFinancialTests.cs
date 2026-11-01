using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Xunit;

namespace Finora.Domain.Tests;

public class TransactionFinancialTests
{
    // ------------------------------------------------------------------
    // Transfer invariant: transfers must not contribute to net household expense/income
    // ------------------------------------------------------------------

    [Fact]
    public void Transfer_TransactionType_ShouldBeExcludedFromNetExpenseCalc()
    {
        // Simulate what analytics would do: exclude Transfer type
        var transactions = new List<Transaction>
        {
            MakeTxn(TransactionType.Expense, 50_000),   // ₹500 expense
            MakeTxn(TransactionType.Income,  200_000),  // ₹2000 income
            MakeTxn(TransactionType.Transfer, 100_000), // ₹1000 transfer — must be excluded
            MakeTxn(TransactionType.Transfer, 100_000), // ₹1000 transfer (dest side — must be excluded)
        };

        var totalExpense = transactions
            .Where(t => t.TransactionType == TransactionType.Expense)
            .Sum(t => t.AmountPaise);

        var totalIncome = transactions
            .Where(t => t.TransactionType == TransactionType.Income)
            .Sum(t => t.AmountPaise);

        // Transfers contribute zero to household net
        Assert.Equal(50_000, totalExpense);
        Assert.Equal(200_000, totalIncome);
    }

    [Fact]
    public void Transfer_BalanceMutations_ShouldBeSymmetric()
    {
        // Both accounts start with 1,000,000 paise (₹10,000)
        var sourceBalance = 1_000_000L;
        var destBalance = 1_000_000L;
        var transferAmount = 300_000L; // ₹3,000

        // Apply transfer
        sourceBalance -= transferAmount;
        destBalance += transferAmount;

        // Net household assets unchanged
        var netBefore = 2_000_000L;
        var netAfter = sourceBalance + destBalance;

        Assert.Equal(netBefore, netAfter);
        Assert.Equal(700_000, sourceBalance);
        Assert.Equal(1_300_000, destBalance);
    }

    // ------------------------------------------------------------------
    // Split validation
    // ------------------------------------------------------------------

    [Fact]
    public void SplitValidation_WhenSplitsSumMatchesTotal_ShouldPass()
    {
        var transactionAmount = 100_000L; // ₹1,000
        var splits = new List<long>
        {
            60_000, // ₹600
            40_000  // ₹400
        };

        var splitSum = splits.Sum();

        Assert.Equal(transactionAmount, splitSum);
    }

    [Fact]
    public void SplitValidation_WhenSplitsDontBalance_ShouldFail()
    {
        var transactionAmount = 100_000L; // ₹1,000
        var splits = new List<long>
        {
            60_000, // ₹600
            30_000  // ₹300 — total = ₹900, not ₹1,000
        };

        var splitSum = splits.Sum();

        Assert.NotEqual(transactionAmount, splitSum);
    }

    [Fact]
    public void SplitValidation_AllSplitsMustBePositive()
    {
        var splits = new List<long> { 50_000, 0, -1_000 };

        Assert.False(splits.All(s => s > 0));
    }

    // ------------------------------------------------------------------
    // Account balance: income/expense effects
    // ------------------------------------------------------------------

    [Fact]
    public void AccountBalance_IncomeIncreasesBalance()
    {
        var balance = 500_000L;
        var incomeAmount = 200_000L;
        balance += incomeAmount;

        Assert.Equal(700_000, balance);
    }

    [Fact]
    public void AccountBalance_ExpenseDecreasesBalance()
    {
        var balance = 500_000L;
        var expenseAmount = 50_000L;
        balance -= expenseAmount;

        Assert.Equal(450_000, balance);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Transaction MakeTxn(TransactionType type, long amountPaise) => new()
    {
        Id = Guid.NewGuid(),
        HouseholdId = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        CreatedByUserId = Guid.NewGuid(),
        TransactionType = type,
        AmountPaise = amountPaise,
        Date = DateOnly.FromDateTime(DateTime.UtcNow),
        Visibility = VisibilityLevel.Household,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}

