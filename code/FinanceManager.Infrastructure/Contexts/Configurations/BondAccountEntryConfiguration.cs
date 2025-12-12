using FinanceManager.Domain.Entities.Bonds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class BondAccountEntryConfiguration : IEntityTypeConfiguration<BondAccountEntry>
{
    public void Configure(EntityTypeBuilder<BondAccountEntry> builder)
    {
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Value)
            .HasPrecision(18, 2);

        builder.Property(e => e.ValueChange)
          .HasPrecision(18, 2);

        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );
        builder.Property(e => e.PostingDate).HasConversion(dateTimeConverter);
    }
}