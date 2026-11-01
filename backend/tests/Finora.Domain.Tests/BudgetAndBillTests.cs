using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Xunit;

namespace Finora.Domain.Tests;

public class BudgetAndBillTests
{
    [Fact]
    public void AdvanceDate_Weekly_Advances7Days()
    {
        var start = new DateOnly(2026, 11, 1);
        var next = RecurringBill.AdvanceDate(start, BillFrequency.Weekly);
        Assert.Equal(new DateOnly(2026, 11, 8), next);
    }

    [Fact]
    public void AdvanceDate_Monthly_Advances1Month()
    {
        var start = new DateOnly(2026, 11, 1);
        var next = RecurringBill.AdvanceDate(start, BillFrequency.Monthly);
        Assert.Equal(new DateOnly(2026, 12, 1), next);
    }

    [Fact]
    public void AdvanceDate_Quarterly_Advances3Months()
    {
        var start = new DateOnly(2026, 11, 1);
        var next = RecurringBill.AdvanceDate(start, BillFrequency.Quarterly);
        Assert.Equal(new DateOnly(2027, 2, 1), next);
    }

    [Fact]
    public void AdvanceDate_Yearly_Advances1Year()
    {
        var start = new DateOnly(2026, 11, 1);
        var next = RecurringBill.AdvanceDate(start, BillFrequency.Yearly);
        Assert.Equal(new DateOnly(2027, 11, 1), next);
    }

    [Fact]
    public void BudgetPercentage_CalculatesAccurately()
    {
        var allocatedPaise = 1_500_000L; // ₹15,000
        var spentPaise = 750_000L;       // ₹7,500

        var pct = Math.Round((double)spentPaise / allocatedPaise * 100, 1);
        Assert.Equal(50.0, pct);
    }

    [Fact]
    public void BudgetPercentage_WhenZeroAllocation_ReturnsZero()
    {
        var allocatedPaise = 0L;
        var spentPaise = 100_000L;

        var pct = allocatedPaise > 0 ? Math.Round((double)spentPaise / allocatedPaise * 100, 1) : 0.0;
        Assert.Equal(0.0, pct);
    }

    [Fact]
    public void GenerateOccurrences_Monthly_GeneratesExpectedCountWithin60Days()
    {
        var today = new DateOnly(2026, 11, 1);
        var bill = new RecurringBill
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Name = "Broadband",
            ExpectedAmountPaise = 99_900,
            Frequency = BillFrequency.Monthly,
            NextDueDate = today
        };

        var windowEnd = today.AddDays(60); // 2026-12-31
        var occurrences = bill.GenerateOccurrences(windowEnd);

        // Due dates will be 2026-11-01, 2026-12-01 (next is 2027-01-01 which is outside window)
        Assert.Equal(2, occurrences.Count);
        Assert.Equal(new DateOnly(2026, 11, 1), occurrences[0].DueDate);
        Assert.Equal(new DateOnly(2026, 12, 1), occurrences[1].DueDate);
    }
}
