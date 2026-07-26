using FinanceManager.Domain.Assets.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class AssetListingConfiguration : IEntityTypeConfiguration<AssetListing>
{
    public void Configure(EntityTypeBuilder<AssetListing> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Ticker).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ExchangeMic).HasMaxLength(16).IsRequired();
        builder.Property(x => x.ExchangeName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TradingCurrency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ListingFigi).HasMaxLength(16);
        builder.Property(x => x.ExchangeInstrumentId).HasMaxLength(64);
        builder.Property(x => x.PriceMultiplier).HasPrecision(18, 8);

        builder.HasIndex(x => new { x.Ticker, x.ExchangeMic, x.TradingCurrency }).IsUnique();
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.ExchangeMic);
        builder.HasIndex(x => x.Ticker);
        builder.HasIndex(x => x.ListingFigi);

        builder.HasMany(x => x.MarketDataSymbols)
            .WithOne(x => x.AssetListing)
            .HasForeignKey(x => x.AssetListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}