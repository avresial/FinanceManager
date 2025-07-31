using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class StockPriceDtoConfiguration : IEntityTypeConfiguration<StockPriceDto>
{
    public void Configure(EntityTypeBuilder<StockPriceDto> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();


        builder.Property(e => e.PricePerUnit)
            .HasPrecision(18, 8);

        // Ensure PostingDate is always returned as UTC
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );
        builder.Property(e => e.Date).HasConversion(dateTimeConverter);
    }
}