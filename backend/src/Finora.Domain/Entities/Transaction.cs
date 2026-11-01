using Finora.Domain.Common;
using Finora.Domain.Enums;

namespace Finora.Domain.Entities;

public class Transaction : IEntity, ITenantScoped, IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public TransactionType TransactionType { get; set; } = TransactionType.Expense;
    public long AmountPaise { get; set; }
    public DateOnly Date { get; set; }
    public string? Payee { get; set; }
    public string? Notes { get; set; }
    public VisibilityLevel Visibility { get; set; } = VisibilityLevel.Household;
    public bool IsSplit { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household? Household { get; set; }
    public Account? Account { get; set; }
    public Category? Category { get; set; }
    public ICollection<TransactionSplit> Splits { get; set; } = new List<TransactionSplit>();
}

