using Finora.Domain.Enums;

namespace Finora.Application.Features.Identity;

public record RegisterInitialAdminRequest(
    string FullName,
    string Email,
    string Password,
    string HouseholdName,
    string SetupToken);

public record RegisterWithInviteRequest(
    string FullName,
    string Password,
    string InviteToken);

public record LoginRequest(
    string Email,
    string Password,
    Guid? HouseholdId = null);

public record RefreshTokenRequest(
    string RefreshToken,
    Guid? HouseholdId = null);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    Guid UserId,
    string Email,
    string FullName,
    Guid? ActiveHouseholdId,
    HouseholdRole? Role);

