using FinanceManager.Domain.Assets.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class PriceQuoteConfiguration : IEntityTypeConfiguration<PriceQuote>
{
    public void Configure(EntityTypeBuilder<PriceQuote> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Price).HasPrecision(18, 8);
        builder.Property(x => x.RawPrice).HasPrecision(18, 8);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.RawCurrency).HasMaxLength(3);

        builder.HasIndex(x => new { x.AssetListingId, x.PriceTime });
        builder.HasIndex(x => x.MarketDataSymbolId);
        builder.HasIndex(x => x.Provider);
        // Only one quote per listing/provider/time/type.
        builder.HasIndex(x => new { x.AssetListingId, x.Provider, x.PriceTime, x.QuoteType }).IsUnique();

        builder.HasOne(x => x.AssetListing)
            .WithMany()
            .HasForeignKey(x => x.AssetListingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional symbol link; do not cascade (avoids multiple cascade paths to PriceQuote on SQL Server).
        builder.HasOne(x => x.MarketDataSymbol)
            .WithMany()
            .HasForeignKey(x => x.MarketDataSymbolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}