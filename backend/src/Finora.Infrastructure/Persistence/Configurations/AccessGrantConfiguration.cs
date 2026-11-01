using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        builder.ToTable("access_grants");

        builder.HasKey(ag => ag.Id);
        builder.Property(ag => ag.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(ag => ag.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(ag => ag.ResourceType).HasColumnName("resource_type").HasMaxLength(32).IsRequired();
        builder.Property(ag => ag.ResourceId).HasColumnName("resource_id").IsRequired();
        builder.Property(ag => ag.GrantedToUserId).HasColumnName("granted_to_user_id").IsRequired();
        builder.Property(ag => ag.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        builder.Property(ag => ag.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(ag => new { ag.ResourceType, ag.ResourceId, ag.GrantedToUserId })
            .IsUnique()
            .HasDatabaseName("ix_access_grants_resource_user");
    }
}

