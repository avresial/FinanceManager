using FinanceManager.Domain.Shared.Maintenance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class MaintenanceApiKeyConfiguration : IEntityTypeConfiguration<MaintenanceApiKey>
{
    public void Configure(EntityTypeBuilder<MaintenanceApiKey> builder)
    {
        builder.HasKey(e => e.Id);

        // SHA-256 rendered as hex is exactly 64 characters.
        builder.Property(e => e.KeyHash)
            .IsRequired()
            .HasMaxLength(64);
    }
}