using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(l => l.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(l => l.OriginalPrincipalPaise).HasColumnName("original_principal_paise").IsRequired();
        builder.Property(l => l.CurrentPrincipalPaise).HasColumnName("current_principal_paise").IsRequired();
        builder.Property(l => l.AnnualInterestRate).HasColumnName("annual_interest_rate").HasPrecision(5, 2).IsRequired();
        builder.Property(l => l.TenureMonths).HasColumnName("tenure_months").IsRequired();
        builder.Property(l => l.MonthlyEmiPaise).HasColumnName("monthly_emi_paise").IsRequired();
        builder.Property(l => l.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(l => new { l.HouseholdId, l.AccountId });

        builder.HasOne(l => l.Household)
            .WithMany()
            .HasForeignKey(l => l.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

