using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class MarketDataSymbolConfiguration : IEntityTypeConfiguration<MarketDataSymbol>
{
    public void Configure(EntityTypeBuilder<MarketDataSymbol> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Symbol).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProviderExchangeCode).HasMaxLength(32);
        builder.Property(x => x.ProviderInstrumentId).HasMaxLength(128);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.LastError).HasMaxLength(512);

        builder.HasIndex(x => new { x.Provider, x.Symbol }).IsUnique();
        builder.HasIndex(x => new { x.AssetListingId, x.Provider, x.Symbol }).IsUnique();
        builder.HasIndex(x => x.AssetListingId);
        builder.HasIndex(x => x.Provider);
        builder.HasIndex(x => x.IsEnabled);
    }
}