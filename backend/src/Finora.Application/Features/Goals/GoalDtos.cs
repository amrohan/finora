using Finora.Domain.Enums;

namespace Finora.Application.Features.Goals;

public record SavingsGoalDto(
    Guid Id,
    string Name,
    long TargetAmountPaise,
    long CurrentAmountPaise,
    double ProgressPercentage,
    DateOnly? TargetDate,
    bool IsCompleted,
    VisibilityLevel Visibility,
    DateTimeOffset CreatedAt);

public record CreateSavingsGoalRequest(
    string Name,
    long TargetAmountPaise,
    DateOnly? TargetDate = null,
    long InitialContributionPaise = 0,
    Guid? SourceAccountId = null,
    VisibilityLevel Visibility = VisibilityLevel.Household);

public record ContributeToGoalRequest(
    Guid SourceAccountId,
    long AmountPaise,
    DateOnly ContributionDate,
    string? Notes = null);

public record GoalContributionDto(
    Guid Id,
    Guid GoalId,
    long AmountPaise,
    DateOnly ContributionDate,
    Guid? TransactionId);

