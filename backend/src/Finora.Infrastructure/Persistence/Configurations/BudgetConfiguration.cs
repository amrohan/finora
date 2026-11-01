using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");

        builder.Property(b => b.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(b => b.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(b => b.Month).HasColumnName("month").IsRequired();
        builder.Property(b => b.Year).HasColumnName("year").IsRequired();
        builder.Property(b => b.AllocatedAmountPaise).HasColumnName("allocated_amount_paise").IsRequired();
        builder.Property(b => b.RolloverEnabled).HasColumnName("rollover_enabled").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(b => new { b.HouseholdId, b.CategoryId, b.Year, b.Month }).IsUnique();

        builder.HasOne(b => b.Household)
            .WithMany()
            .HasForeignKey(b => b.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

