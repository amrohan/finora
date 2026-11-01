using Finora.Domain.Enums;

namespace Finora.Application.Features.Transactions;

public record TransactionSplitDto(
    Guid CategoryId,
    string? CategoryName,
    long AmountPaise,
    string? Notes);

public record TransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    Guid? CategoryId,
    string? CategoryName,
    TransactionType TransactionType,
    long AmountPaise,
    DateOnly Date,
    string? Payee,
    string? Notes,
    VisibilityLevel Visibility,
    bool IsSplit,
    IReadOnlyList<TransactionSplitDto>? Splits,
    DateTimeOffset CreatedAt);

public record CreateTransactionSplitRequest(
    Guid CategoryId,
    long AmountPaise,
    string? Notes);

public record CreateTransactionRequest(
    Guid AccountId,
    Guid? CategoryId,
    TransactionType TransactionType,
    long AmountPaise,
    DateOnly Date,
    string? Payee = null,
    string? Notes = null,
    VisibilityLevel Visibility = VisibilityLevel.Household,
    IReadOnlyList<CreateTransactionSplitRequest>? Splits = null);

public record RecordTransferRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    long AmountPaise,
    DateOnly Date,
    string? Notes = null);

