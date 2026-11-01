using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.HouseholdId).HasColumnName("household_id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(a => a.EntityName).HasColumnName("entity_name").HasMaxLength(64).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(64).IsRequired();
        builder.Property(a => a.OldValuesJson).HasColumnName("old_values_json").HasColumnType("jsonb");
        builder.Property(a => a.NewValuesJson).HasColumnName("new_values_json").HasColumnType("jsonb");
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(a => a.Timestamp).HasColumnName("timestamp").IsRequired();

        builder.HasIndex(a => new { a.HouseholdId, a.Timestamp });
    }
}

