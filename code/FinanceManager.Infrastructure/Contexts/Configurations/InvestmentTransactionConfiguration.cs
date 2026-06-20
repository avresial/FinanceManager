using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class InvestmentTransactionConfiguration : IEntityTypeConfiguration<InvestmentTransaction>
{
    public void Configure(EntityTypeBuilder<InvestmentTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Quantity).HasPrecision(18, 8);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 8);
        builder.Property(x => x.Fee).HasPrecision(18, 8);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(512);

        builder.Ignore(x => x.SignedQuantity);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.AssetListingId);
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => new { x.UserId, x.TradeDate });
        builder.HasIndex(x => new { x.UserId, x.AssetListingId });
    }
}