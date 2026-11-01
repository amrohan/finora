using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class Transfer : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid SourceTransactionId { get; set; }
    public Guid DestinationTransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
    public Transaction? SourceTransaction { get; set; }
    public Transaction? DestinationTransaction { get; set; }
}

