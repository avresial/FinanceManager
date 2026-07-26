using FinanceManager.Domain.Shared.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class AiProviderConfigurationConfiguration : IEntityTypeConfiguration<AiProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<AiProviderConfiguration> builder)
    {
        builder.HasKey(e => e.ProviderName);

        builder.Property(e => e.ProviderName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.BaseUrl)
            .HasMaxLength(500);

        builder.Property(e => e.ApiKey)
            .HasMaxLength(1000);
    }
}