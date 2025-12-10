using FinanceManager.Domain.Entities.Stocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class StockAccountEntryConfiguration : IEntityTypeConfiguration<StockAccountEntry>
{
    public void Configure(EntityTypeBuilder<StockAccountEntry> builder)
    {
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Value)
          .HasPrecision(18, 8);

        builder.Property(e => e.ValueChange)
         .HasPrecision(18, 8);

        // Ensure PostingDate is always returned as UTC
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );
        builder.Property(e => e.PostingDate).HasConversion(dateTimeConverter);
    }
}