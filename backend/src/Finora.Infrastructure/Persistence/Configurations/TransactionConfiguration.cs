using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(t => t.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(t => t.CategoryId).HasColumnName("category_id");
        builder.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(t => t.TransactionType).HasColumnName("transaction_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(t => t.AmountPaise).HasColumnName("amount_paise").IsRequired();
        builder.Property(t => t.Date).HasColumnName("date").HasColumnType("date").IsRequired();
        builder.Property(t => t.Payee).HasColumnName("payee").HasMaxLength(128);
        builder.Property(t => t.Notes).HasColumnName("notes");
        builder.Property(t => t.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(t => t.IsSplit).HasColumnName("is_split").IsRequired().HasDefaultValue(false);
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.HouseholdId, t.Date }).HasDatabaseName("ix_transactions_household_date");
        builder.HasIndex(t => t.AccountId).HasDatabaseName("ix_transactions_account");
    }
}

