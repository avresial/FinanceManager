using FinanceManager.Domain.Transactions.Bonds.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class BondEntryConfiguration : IEntityTypeConfiguration<BondEntry>
{
    public void Configure(EntityTypeBuilder<BondEntry> builder)
    {
        // Physical names are pinned because they predate the BondEntry type name.
        builder.ToTable("BondEntries");
        builder.HasMany(e => e.Labels)
            .WithOne()
            .HasForeignKey("BondAccountEntryEntryId");
        builder.Property(e => e.UserId).IsRequired();
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ValueChange)
          .HasPrecision(18, 2);

        builder.Property(e => e.Description).HasMaxLength(512).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.BondDetailsId, x.PostingDate, x.EntryId })
            .HasDatabaseName("IX_BondEntries_OwnerBondDateId");
        builder.HasIndex(x => new { x.UserId, x.PostingDate, x.EntryId })
            .HasDatabaseName("IX_BondEntries_OwnerDateId");

        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );
        builder.Property(e => e.PostingDate).HasConversion(dateTimeConverter);
    }
}