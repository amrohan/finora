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

public record ParsedFileDto(
    string FileName,
    int TotalRows,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> PreviewRows,
    IReadOnlyList<IReadOnlyDictionary<string, string>> AllRows);

public record ImportTransactionItemDto(
    DateOnly Date,
    string? Payee,
    long AmountPaise,
    TransactionType TransactionType,
    Guid? CategoryId = null,
    string? Notes = null,
    VisibilityLevel Visibility = VisibilityLevel.Household);

public record ExecuteImportRequest(
    Guid AccountId,
    IReadOnlyList<ImportTransactionItemDto> Transactions,
    bool SkipDuplicates = true);

public record ExecuteImportResponse(
    int ImportedCount,
    int SkippedCount,
    long NewAccountBalancePaise);

public record BulkCategorizeRequest(
    IReadOnlyList<Guid> TransactionIds,
    Guid? CategoryId);

public record BulkDeleteRequest(
    IReadOnlyList<Guid> TransactionIds);

public record BulkVisibilityRequest(
    IReadOnlyList<Guid> TransactionIds,
    VisibilityLevel Visibility);

public record BulkActionResultResponse(
    int AffectedCount);



