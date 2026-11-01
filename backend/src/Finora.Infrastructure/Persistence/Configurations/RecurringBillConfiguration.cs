using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class RecurringBillConfiguration : IEntityTypeConfiguration<RecurringBill>
{
    public void Configure(EntityTypeBuilder<RecurringBill> builder)
    {
        builder.ToTable("recurring_bills");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");

        builder.Property(b => b.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(b => b.CategoryId).HasColumnName("category_id");
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(b => b.ExpectedAmountPaise).HasColumnName("expected_amount_paise").IsRequired();
        builder.Property(b => b.Frequency).HasColumnName("frequency").IsRequired();
        builder.Property(b => b.NextDueDate).HasColumnName("next_due_date").IsRequired();
        builder.Property(b => b.AutoRecord).HasColumnName("auto_record").IsRequired();
        builder.Property(b => b.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(b => new { b.HouseholdId, b.IsActive });

        builder.HasOne(b => b.Household)
            .WithMany()
            .HasForeignKey(b => b.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

