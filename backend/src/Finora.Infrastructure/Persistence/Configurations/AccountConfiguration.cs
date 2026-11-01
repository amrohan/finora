using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(a => a.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.CurrentBalancePaise).HasColumnName("current_balance_paise").IsRequired().HasDefaultValue(0L);
        builder.Property(a => a.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired().HasDefaultValue("INR");
        builder.Property(a => a.AccountNumberMask).HasColumnName("account_number_mask").HasMaxLength(8);
        builder.Property(a => a.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.IsArchived).HasColumnName("is_archived").IsRequired().HasDefaultValue(false);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(a => new { a.HouseholdId, a.OwnerUserId }).HasDatabaseName("ix_accounts_household_owner");
    }
}

