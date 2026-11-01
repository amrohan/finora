using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class TransactionSplitConfiguration : IEntityTypeConfiguration<TransactionSplit>
{
    public void Configure(EntityTypeBuilder<TransactionSplit> builder)
    {
        builder.ToTable("transaction_splits");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(s => s.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(s => s.TransactionId).HasColumnName("transaction_id").IsRequired();
        builder.Property(s => s.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(s => s.AmountPaise).HasColumnName("amount_paise").IsRequired();
        builder.Property(s => s.Notes).HasColumnName("notes").HasMaxLength(256);

        builder.HasOne(s => s.Transaction)
            .WithMany(t => t.Splits)
            .HasForeignKey(s => s.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Category)
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.HouseholdId, s.TransactionId }).HasDatabaseName("ix_transaction_splits_household_tx");
    }
}

