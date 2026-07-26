using FinanceManager.Domain.Assets.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class AssetIdentifierConfiguration : IEntityTypeConfiguration<AssetIdentifier>
{
    public void Configure(EntityTypeBuilder<AssetIdentifier> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Value).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(64);

        builder.HasIndex(x => new { x.Type, x.Value }).IsUnique();
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.Value);
    }
}