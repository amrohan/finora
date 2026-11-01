using Finora.Domain.Common;
using Finora.Domain.Enums;

namespace Finora.Domain.Entities;

public class HouseholdMember : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public HouseholdRole Role { get; set; } = HouseholdRole.Member;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
}

