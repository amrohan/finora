using Finora.Domain.Common;
using Finora.Domain.Enums;

namespace Finora.Domain.Entities;

public class SavingsGoal : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long TargetAmountPaise { get; set; }
    public long CurrentAmountPaise { get; set; }
    public DateOnly? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public VisibilityLevel Visibility { get; set; } = VisibilityLevel.Household;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
    public ICollection<GoalContribution> Contributions { get; set; } = new List<GoalContribution>();
}

