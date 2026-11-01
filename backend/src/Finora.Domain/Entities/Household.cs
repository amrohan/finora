using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class Household : IEntity, IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
    public string Timezone { get; set; } = "Asia/Kolkata";
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
}

