using Finora.Domain.Common;
using Finora.Domain.Enums;

namespace Finora.Domain.Entities;

public class Invitation : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Email { get; set; } = string.Empty;
    public HouseholdRole Role { get; set; } = HouseholdRole.Member;
    public string TokenHash { get; set; } = string.Empty;
    public Guid InvitedByUserId { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }

    public bool IsValid => Status == InvitationStatus.Pending && ExpiresAt > DateTimeOffset.UtcNow;
}

