using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class InvestmentTransactionConfiguration : IEntityTypeConfiguration<InvestmentTransaction>
{
    public void Configure(EntityTypeBuilder<InvestmentTransaction> builder)
    {
        builder.HasKey(x => x.EntryId);
        builder.Property(x => x.EntryId).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.Property(x => x.Quantity).HasPrecision(18, 8);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 8);
        builder.Property(x => x.Fee).HasPrecision(18, 8);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512).IsRequired();

        builder.Ignore(x => x.TradeDate);
        builder.Property(x => x.PostingDate).HasConversion(new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
        builder.Ignore(x => x.SignedQuantity);
        builder.Ignore(x => x.ValueChange);

        // Transactions link to an asset listing; the Assets feature does not back-reference user transactions.
        // Keep transactions if a listing is ever removed by requiring the listing to be transaction-free first.
        builder.HasOne(x => x.AssetListing)
            .WithMany()
            .HasForeignKey(x => x.AssetListingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.AssetListingId);
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => new { x.AccountId, x.PostingDate, x.EntryId });
        builder.HasIndex(x => new { x.UserId, x.PostingDate });
        builder.HasIndex(x => new { x.UserId, x.AssetListingId });
    }
}