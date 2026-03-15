using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

internal class FinancialLabelConfiguration : IEntityTypeConfiguration<FinancialLabel>
{
    public void Configure(EntityTypeBuilder<FinancialLabel> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasMany(e => e.Classifications)
            .WithOne()
            .HasForeignKey(e => e.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }

}