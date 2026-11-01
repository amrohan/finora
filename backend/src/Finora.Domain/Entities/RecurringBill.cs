using Finora.Domain.Common;
using Finora.Domain.Enums;

namespace Finora.Domain.Entities;

public class RecurringBill : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long ExpectedAmountPaise { get; set; }
    public BillFrequency Frequency { get; set; } = BillFrequency.Monthly;
    public DateOnly NextDueDate { get; set; }
    public bool AutoRecord { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
    public Category? Category { get; set; }
    public ICollection<BillOccurrence> Occurrences { get; set; } = new List<BillOccurrence>();

    public static DateOnly AdvanceDate(DateOnly current, BillFrequency frequency) => frequency switch
    {
        BillFrequency.Weekly => current.AddDays(7),
        BillFrequency.Monthly => current.AddMonths(1),
        BillFrequency.Quarterly => current.AddMonths(3),
        BillFrequency.Yearly => current.AddYears(1),
        _ => current.AddMonths(1)
    };

    public List<BillOccurrence> GenerateOccurrences(DateOnly windowEnd)
    {
        var list = new List<BillOccurrence>();
        var currentDue = NextDueDate;

        while (currentDue <= windowEnd)
        {
            list.Add(new BillOccurrence
            {
                Id = Guid.NewGuid(),
                HouseholdId = HouseholdId,
                BillId = Id,
                DueDate = currentDue,
                Status = BillOccurrenceStatus.Upcoming
            });

            currentDue = AdvanceDate(currentDue, Frequency);
        }

        return list;
    }
}
