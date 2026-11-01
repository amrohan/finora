using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class AuditLog : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? HouseholdId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

