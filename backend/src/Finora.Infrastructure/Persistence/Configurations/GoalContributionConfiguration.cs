using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class GoalContributionConfiguration : IEntityTypeConfiguration<GoalContribution>
{
    public void Configure(EntityTypeBuilder<GoalContribution> builder)
    {
        builder.ToTable("goal_contributions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(c => c.GoalId).HasColumnName("goal_id").IsRequired();
        builder.Property(c => c.TransactionId).HasColumnName("transaction_id");
        builder.Property(c => c.AmountPaise).HasColumnName("amount_paise").IsRequired();
        builder.Property(c => c.ContributionDate).HasColumnName("contribution_date").IsRequired();

        builder.HasIndex(c => new { c.HouseholdId, c.GoalId, c.ContributionDate });

        builder.HasOne(c => c.Household)
            .WithMany()
            .HasForeignKey(c => c.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Goal)
            .WithMany(g => g.Contributions)
            .HasForeignKey(c => c.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Transaction)
            .WithMany()
            .HasForeignKey(c => c.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

