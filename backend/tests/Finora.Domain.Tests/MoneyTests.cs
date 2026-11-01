using Finora.Domain.ValueObjects;
using Xunit;

namespace Finora.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void FromPaise_ShouldStoreExactPaise()
    {
        var money = Money.FromPaise(125050);
        Assert.Equal(125050, money.Paise);
        Assert.Equal(1250.50m, money.ToRupees());
    }

    [Fact]
    public void FromRupees_ShouldConvertAccuratelyToPaise()
    {
        var money = Money.FromRupees(1500.75m);
        Assert.Equal(150075, money.Paise);
        Assert.Equal(1500.75m, money.ToRupees());
    }

    [Fact]
    public void Addition_ShouldHaveZeroFloatingPointDrift()
    {
        // 0.10 + 0.20 in float is 0.30000000000000004
        // In Money (paise), 10 paise + 20 paise = 30 paise
        var m1 = Money.FromRupees(0.10m);
        var m2 = Money.FromRupees(0.20m);
        var sum = m1 + m2;

        Assert.Equal(30, sum.Paise);
        Assert.Equal(0.30m, sum.ToRupees());
    }

    [Fact]
    public void Subtraction_ShouldCalculateExactDifference()
    {
        var balance = Money.FromRupees(1000m);
        var expense = Money.FromRupees(350.50m);
        var remaining = balance - expense;

        Assert.Equal(64950, remaining.Paise);
        Assert.Equal(649.50m, remaining.ToRupees());
    }

    [Fact]
    public void Comparisons_ShouldWorkCorrectly()
    {
        var small = Money.FromPaise(100);
        var large = Money.FromPaise(200);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.False(small >= large);
        Assert.True(small <= large);
    }

    [Fact]
    public void Formatting_ShouldRenderIndianCurrencyFormat()
    {
        var amount = Money.FromPaise(10000000); // 1 Lakh = ₹ 1,00,000.00
        var formatted = amount.ToFormattedString();

        Assert.Contains("1,00,000.00", formatted);
    }
}

