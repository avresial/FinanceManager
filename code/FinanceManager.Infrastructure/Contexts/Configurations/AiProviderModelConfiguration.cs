using FinanceManager.Domain.Shared.Ai.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class AiProviderModelConfiguration : IEntityTypeConfiguration<AiProviderModel>
{
    public void Configure(EntityTypeBuilder<AiProviderModel> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.ProviderName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ModelName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(e => e.ProviderName);
    }
}