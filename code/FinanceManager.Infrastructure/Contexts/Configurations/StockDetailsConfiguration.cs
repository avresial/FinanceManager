using FinanceManager.Domain.Entities.Stocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class StockDetailsConfiguration : IEntityTypeConfiguration<StockDetails>
{
    public void Configure(EntityTypeBuilder<StockDetails> builder)
    {
        // Phase 3c: ISIN is now the primary key
        builder.HasKey(x => x.Isin);
        builder.Property(x => x.Isin).HasMaxLength(12).IsRequired();
        builder.Property(x => x.Ticker).HasMaxLength(32).IsRequired();
        builder.Property(x => x.AlphaVantageSymbol).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(256);
        builder.Property(x => x.Type).HasMaxLength(64);
        builder.Property(x => x.Region).HasMaxLength(128);
        builder.HasOne(x => x.Currency)
            .WithMany()
            .IsRequired();

        // Index on Ticker for legacy lookups during API compatibility period
        builder.HasIndex(x => x.Ticker);
    }
}