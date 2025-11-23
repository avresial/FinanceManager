using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

internal class FinancialLabelConfiguration : IEntityTypeConfiguration<FinancialLabel>
{
    public void Configure(EntityTypeBuilder<FinancialLabel> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

    }

}