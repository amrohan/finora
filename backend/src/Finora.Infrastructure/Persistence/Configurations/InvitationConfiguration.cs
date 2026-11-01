using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(i => i.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(i => i.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(i => i.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(i => i.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(i => i.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.HouseholdId, i.Email });
    }
}

