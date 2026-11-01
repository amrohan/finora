using Finora.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finora.Infrastructure.Persistence.Configurations;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.HouseholdId).HasColumnName("household_id").IsRequired();
        builder.Property(t => t.SourceTransactionId).HasColumnName("source_transaction_id").IsRequired();
        builder.Property(t => t.DestinationTransactionId).HasColumnName("destination_transaction_id").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(t => t.SourceTransaction)
            .WithOne()
            .HasForeignKey<Transfer>(t => t.SourceTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.DestinationTransaction)
            .WithOne()
            .HasForeignKey<Transfer>(t => t.DestinationTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.SourceTransactionId).IsUnique();
        builder.HasIndex(t => t.DestinationTransactionId).IsUnique();
    }
}

