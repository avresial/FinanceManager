using FinanceManager.Domain.Entities.Stocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class StockDetailsConfiguration : IEntityTypeConfiguration<StockDetails>
{
    public void Configure(EntityTypeBuilder<StockDetails> builder)
    {
        // Phase 3: Keep Ticker as PK temporarily, Isin will become PK in Phase 3b after backfill
        builder.HasKey(x => x.Ticker);
        builder.Property(x => x.Ticker).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Isin).HasMaxLength(12); // Nullable until Phase 3b
        builder.Property(x => x.Name).HasMaxLength(256);
        builder.Property(x => x.Type).HasMaxLength(64);
        builder.Property(x => x.Region).HasMaxLength(128);
        builder.HasOne(x => x.Currency)
            .WithMany()
            .IsRequired();

        // Add unique constraint on Isin for future PK switch
        builder.HasIndex(x => x.Isin).IsUnique(false);
    }
}