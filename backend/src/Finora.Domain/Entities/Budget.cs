using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class Budget : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid CategoryId { get; set; }
    public int Month { get; set; } // 1-12
    public int Year { get; set; }  // >= 2020
    public long AllocatedAmountPaise { get; set; }
    public bool RolloverEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
    public Category? Category { get; set; }
}

