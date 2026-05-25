using FinanceManager.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class AiFallbackEntryConfiguration : IEntityTypeConfiguration<AiFallbackEntry>
{
    public void Configure(EntityTypeBuilder<AiFallbackEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.ProviderName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Model)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(e => e.Order);
    }
}