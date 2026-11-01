using Finora.Domain.Entities;
using Finora.Domain.Enums;

namespace Finora.Application.Common;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    Guid? HouseholdId { get; }
    HouseholdRole? Role { get; }
    bool IsAuthenticated { get; }
}

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, Guid? activeHouseholdId, HouseholdRole? role);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}

public interface IAuditService
{
    Task LogAsync(
        Guid? householdId,
        Guid? userId,
        string action,
        string entityName,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}

