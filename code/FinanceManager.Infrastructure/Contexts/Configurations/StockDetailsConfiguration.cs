using FinanceManager.Domain.Entities.Stocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class StockDetailsConfiguration : IEntityTypeConfiguration<StockDetails>
{
    public void Configure(EntityTypeBuilder<StockDetails> builder)
    {
        builder.HasKey(x => x.Ticker);
        builder.Property(x => x.Ticker).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256);
        builder.Property(x => x.Type).HasMaxLength(64);
        builder.Property(x => x.Region).HasMaxLength(128);
        builder.HasOne(x => x.Currency)
            .WithMany()
            .IsRequired();
    }
}