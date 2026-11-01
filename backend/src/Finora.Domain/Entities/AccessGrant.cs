using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class AccessGrant : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string ResourceType { get; set; } = string.Empty; // "Account", "Transaction"
    public Guid ResourceId { get; set; }
    public Guid GrantedToUserId { get; set; }
    public Guid GrantedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
}

