using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class TransactionSplit : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid CategoryId { get; set; }
    public long AmountPaise { get; set; }
    public string? Notes { get; set; }

    public Household? Household { get; set; }
    public Transaction? Transaction { get; set; }
    public Category? Category { get; set; }
}

