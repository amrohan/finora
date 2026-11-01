using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("households");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(h => h.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(h => h.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(h => h.Timezone).HasColumnName("timezone").HasMaxLength(64).IsRequired();
        builder.Property(h => h.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(h => h.Members)
            .WithOne(m => m.Household)
            .HasForeignKey(m => m.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(h => h.Invitations)
            .WithOne(i => i.Household)
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

