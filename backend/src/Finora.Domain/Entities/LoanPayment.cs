using Finora.Domain.Common;

namespace Finora.Domain.Entities;

public class LoanPayment : IEntity, ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid LoanId { get; set; }
    public Guid TransactionId { get; set; }
    public long PrincipalPaise { get; set; }
    public long InterestPaise { get; set; }
    public DateOnly PaymentDate { get; set; }

    public Household? Household { get; set; }
    public Loan? Loan { get; set; }
    public Transaction? Transaction { get; set; }
}

