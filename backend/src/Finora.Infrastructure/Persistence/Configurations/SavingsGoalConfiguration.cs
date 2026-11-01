using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
{
    public void Configure(EntityTypeBuilder<SavingsGoal> builder)
    {
        builder.ToTable("savings_goals");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");

        builder.Property(g => g.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(g => g.TargetAmountPaise).HasColumnName("target_amount_paise").IsRequired();
        builder.Property(g => g.CurrentAmountPaise).HasColumnName("current_amount_paise").IsRequired();
        builder.Property(g => g.TargetDate).HasColumnName("target_date");
        builder.Property(g => g.IsCompleted).HasColumnName("is_completed").IsRequired();
        builder.Property(g => g.Visibility).HasColumnName("visibility").IsRequired();
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(g => new { g.HouseholdId, g.IsCompleted });

        builder.HasOne(g => g.Household)
            .WithMany()
            .HasForeignKey(g => g.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

