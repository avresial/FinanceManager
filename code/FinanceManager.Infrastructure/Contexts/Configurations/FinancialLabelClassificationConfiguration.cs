using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

internal class FinancialLabelClassificationConfiguration : IEntityTypeConfiguration<FinancialLabelClassification>
{
    public void Configure(EntityTypeBuilder<FinancialLabelClassification> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Kind)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Value)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.LabelId, e.Kind })
            .IsUnique();
    }
}