using System.Text.Json;
using Finora.Application.Common;
using Finora.Domain.Entities;
using Finora.Infrastructure.Persistence;

namespace Finora.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly FinoraDbContext _dbContext;

    public AuditService(FinoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        Guid? householdId,
        Guid? userId,
        string action,
        string entityName,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            HouseholdId = householdId,
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            Timestamp = DateTimeOffset.UtcNow
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

