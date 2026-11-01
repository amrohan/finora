using Finora.Domain.Common;
using Finora.Domain.Enums;

namespace Finora.Domain.Entities;

public class Account : IEntity, ITenantScoped, IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Savings;
    public long CurrentBalancePaise { get; set; }
    public string Currency { get; set; } = "INR";
    public string? AccountNumberMask { get; set; }
    public VisibilityLevel Visibility { get; set; } = VisibilityLevel.Household;
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

