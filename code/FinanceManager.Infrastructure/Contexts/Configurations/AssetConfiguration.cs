using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Isin).HasMaxLength(12);
        builder.Property(x => x.ShareClassFigi).HasMaxLength(16);
        builder.Property(x => x.CompositeFigi).HasMaxLength(16);
        builder.Property(x => x.Issuer).HasMaxLength(128);
        builder.Property(x => x.Domicile).HasMaxLength(64);
        builder.Property(x => x.BaseCurrency).HasMaxLength(3);
        builder.Property(x => x.BenchmarkIndex).HasMaxLength(128);
        builder.Property(x => x.TotalExpenseRatio).HasPrecision(18, 8);

        // Unique on ISIN. The column is nullable; EF emits a NULL-excluding filtered index on SQL Server
        // and PostgreSQL treats NULLs as distinct, so multiple assets without an ISIN are still allowed.
        builder.HasIndex(x => x.Isin).IsUnique();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Type);

        builder.HasMany(x => x.Listings)
            .WithOne(x => x.Asset)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Identifiers)
            .WithOne(x => x.Asset)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}