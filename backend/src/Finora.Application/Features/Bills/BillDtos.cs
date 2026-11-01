using Finora.Domain.Enums;

namespace Finora.Application.Features.Bills;

public record RecurringBillDto(
    Guid Id,
    Guid? CategoryId,
    string? CategoryName,
    string Name,
    long ExpectedAmountPaise,
    BillFrequency Frequency,
    DateOnly NextDueDate,
    bool AutoRecord,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CreateRecurringBillRequest(
    string Name,
    long ExpectedAmountPaise,
    BillFrequency Frequency,
    DateOnly NextDueDate,
    Guid? CategoryId = null,
    bool AutoRecord = false);

public record UpdateRecurringBillRequest(
    string Name,
    long ExpectedAmountPaise,
    BillFrequency Frequency,
    DateOnly NextDueDate,
    Guid? CategoryId = null,
    bool IsActive = true);

public record BillOccurrenceDto(
    Guid Id,
    Guid BillId,
    string BillName,
    long AmountPaise,
    DateOnly DueDate,
    BillOccurrenceStatus Status,
    Guid? TransactionId,
    DateTimeOffset? PaidAt);

public record PayBillOccurrenceRequest(
    Guid AccountId,
    DateOnly PaymentDate,
    string? Notes = null);

