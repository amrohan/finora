using System.Globalization;

namespace Finora.Domain.ValueObjects;

public readonly record struct Money : IComparable<Money>
{
    public long Paise { get; }

    public Money(long paise)
    {
        Paise = paise;
    }

    public static Money Zero => new(0);

    public static Money FromPaise(long paise) => new(paise);

    public static Money FromRupees(decimal rupees)
    {
        // 1 Rupee = 100 paise
        long paise = (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);
        return new Money(paise);
    }

    public decimal ToRupees() => Paise / 100m;

    public bool IsZero => Paise == 0;
    public bool IsNegative => Paise < 0;
    public bool IsPositive => Paise > 0;

    public static Money operator +(Money a, Money b) => new(a.Paise + b.Paise);
    public static Money operator -(Money a, Money b) => new(a.Paise - b.Paise);
    public static Money operator *(Money a, int multiplier) => new(a.Paise * multiplier);
    public static Money operator -(Money a) => new(-a.Paise);

    public static bool operator <(Money a, Money b) => a.Paise < b.Paise;
    public static bool operator >(Money a, Money b) => a.Paise > b.Paise;
    public static bool operator <=(Money a, Money b) => a.Paise <= b.Paise;
    public static bool operator >=(Money a, Money b) => a.Paise >= b.Paise;

    public int CompareTo(Money other) => Paise.CompareTo(other.Paise);

    /// <summary>
    /// Formats amount in Indian Rupee format (e.g. ₹ 1,50,000.50 or -₹ 2,400.00).
    /// </summary>
    public string ToFormattedString()
    {
        var culture = CultureInfo.GetCultureInfo("en-IN");
        return ToRupees().ToString("C2", culture);
    }

    public override string ToString() => ToFormattedString();
}

