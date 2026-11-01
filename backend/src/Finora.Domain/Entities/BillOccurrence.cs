using Finora.Domain.Common;
using Finora.Domain.Enums;

namespace Finora.Domain.Entities;

public class BillOccurrence : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid BillId { get; set; }
    public DateOnly DueDate { get; set; }
    public BillOccurrenceStatus Status { get; set; } = BillOccurrenceStatus.Upcoming;
    public Guid? TransactionId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    public Household? Household { get; set; }
    public RecurringBill? RecurringBill { get; set; }
    public Transaction? Transaction { get; set; }
}

