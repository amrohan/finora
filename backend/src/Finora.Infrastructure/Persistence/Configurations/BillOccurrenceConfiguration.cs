using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class BillOccurrenceConfiguration : IEntityTypeConfiguration<BillOccurrence>
{
    public void Configure(EntityTypeBuilder<BillOccurrence> builder)
    {
        builder.ToTable("bill_occurrences");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");

        builder.Property(o => o.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(o => o.BillId).HasColumnName("bill_id").IsRequired();
        builder.Property(o => o.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(o => o.Status).HasColumnName("status").IsRequired();
        builder.Property(o => o.TransactionId).HasColumnName("transaction_id");
        builder.Property(o => o.PaidAt).HasColumnName("paid_at");

        builder.HasIndex(o => new { o.HouseholdId, o.DueDate, o.Status });

        builder.HasOne(o => o.Household)
            .WithMany()
            .HasForeignKey(o => o.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.RecurringBill)
            .WithMany(b => b.Occurrences)
            .HasForeignKey(o => o.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Transaction)
            .WithMany()
            .HasForeignKey(o => o.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

