using FinanceManager.Domain.Shared.ExternalServices.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class ExternalServiceConfigurationConfiguration : IEntityTypeConfiguration<ExternalServiceConfiguration>
{
    public void Configure(EntityTypeBuilder<ExternalServiceConfiguration> builder)
    {
        builder.HasKey(e => e.ServiceName);

        builder.Property(e => e.ServiceName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.BaseUrl)
            .HasMaxLength(500);

        builder.Property(e => e.ApiKey)
            .HasMaxLength(1000);
    }
}