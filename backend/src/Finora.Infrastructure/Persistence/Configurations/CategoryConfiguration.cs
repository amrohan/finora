using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(c => c.ParentCategoryId).HasColumnName("parent_category_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(c => c.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Icon).HasColumnName("icon").HasMaxLength(64);
        builder.Property(c => c.ColorHex).HasColumnName("color_hex").HasMaxLength(7);
        builder.Property(c => c.IsSystem).HasColumnName("is_system").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(c => c.ParentCategory)
            .WithMany(p => p.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.HouseholdId, c.Name }).HasDatabaseName("ix_categories_household_name");
    }
}

