using Finora.Domain.Enums;

namespace Finora.Application.Features.Accounts;

public record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    long CurrentBalancePaise,
    string Currency,
    string? AccountNumberMask,
    VisibilityLevel Visibility,
    bool IsOwner,
    string? OwnerFullName,
    bool IsArchived,
    DateTimeOffset CreatedAt);

public record CreateAccountRequest(
    string Name,
    AccountType Type = AccountType.Savings,
    long OpeningBalancePaise = 0,
    string Currency = "INR",
    string? AccountNumberMask = null,
    VisibilityLevel Visibility = VisibilityLevel.Household);

public record UpdateAccountRequest(
    string Name,
    string? AccountNumberMask = null,
    VisibilityLevel Visibility = VisibilityLevel.Household,
    bool IsArchived = false);

