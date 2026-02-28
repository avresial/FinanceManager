using FinanceManager.Domain.Entities.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class CsvHeaderMappingConfiguration : IEntityTypeConfiguration<CsvHeaderMapping>
{
    public void Configure(EntityTypeBuilder<CsvHeaderMapping> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.HeaderName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.FieldName)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => new { e.HeaderName, e.FieldName })
            .IsUnique()
            .HasDatabaseName("IX_CsvHeaderMapping_Header_Field");

        builder.HasIndex(e => e.HeaderName)
            .HasDatabaseName("IX_CsvHeaderMapping_Header");
    }
}
