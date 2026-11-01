using Finora.Domain.Enums;

namespace Finora.Application.Features.Households;

public record HouseholdDto(
    Guid Id,
    string Name,
    string Currency,
    string Timezone,
    HouseholdRole Role,
    int MemberCount,
    DateTimeOffset CreatedAt);

public record CreateHouseholdRequest(
    string Name,
    string Currency = "INR",
    string Timezone = "Asia/Kolkata");

public record HouseholdMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    HouseholdRole Role,
    DateTimeOffset JoinedAt);

public record InviteMemberRequest(
    string Email,
    HouseholdRole Role = HouseholdRole.Member);

public record InvitationDto(
    Guid Id,
    string Email,
    HouseholdRole Role,
    InvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string? InviteToken = null);

