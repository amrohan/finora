using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class LoanPaymentConfiguration : IEntityTypeConfiguration<LoanPayment>
{
    public void Configure(EntityTypeBuilder<LoanPayment> builder)
    {
        builder.ToTable("loan_payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(p => p.LoanId).HasColumnName("loan_id").IsRequired();
        builder.Property(p => p.TransactionId).HasColumnName("transaction_id").IsRequired();
        builder.Property(p => p.PrincipalPaise).HasColumnName("principal_paise").IsRequired();
        builder.Property(p => p.InterestPaise).HasColumnName("interest_paise").IsRequired();
        builder.Property(p => p.PaymentDate).HasColumnName("payment_date").IsRequired();

        builder.HasIndex(p => new { p.HouseholdId, p.LoanId, p.PaymentDate });

        builder.HasOne(p => p.Household)
            .WithMany()
            .HasForeignKey(p => p.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Loan)
            .WithMany(l => l.Payments)
            .HasForeignKey(p => p.LoanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Transaction)
            .WithMany()
            .HasForeignKey(p => p.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

