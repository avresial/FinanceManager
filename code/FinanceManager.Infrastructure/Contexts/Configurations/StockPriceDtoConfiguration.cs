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

        builder.HasOne(e => e.StockDetails)
            .WithMany()
            .HasForeignKey("StockIsin")
            .IsRequired();

        builder.Property<string>("StockIsin")
            .HasMaxLength(12);

        builder.HasIndex("StockIsin", nameof(StockPriceDto.Date));

        builder.Property(e => e.PricePerUnit)
            .HasPrecision(18, 8);

        // Stock prices are date-only values; preserve the calendar day instead of timezone-shifting midnight.
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v.Date, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v.Date, DateTimeKind.Utc)
        );
        builder.Property(e => e.Date).HasConversion(dateTimeConverter);
    }
}
