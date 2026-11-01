using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class GoalContribution : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid GoalId { get; set; }
    public Guid? TransactionId { get; set; }
    public long AmountPaise { get; set; }
    public DateOnly ContributionDate { get; set; }

    public Household? Household { get; set; }
    public SavingsGoal? Goal { get; set; }
    public Transaction? Transaction { get; set; }
}

