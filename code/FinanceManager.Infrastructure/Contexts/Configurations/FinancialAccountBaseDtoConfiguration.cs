using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class FinancialAccountBaseDtoConfiguration : IEntityTypeConfiguration<FinancialAccountBaseDto>
{
    public void Configure(EntityTypeBuilder<FinancialAccountBaseDto> builder)
    {
        builder.HasKey(e => e.AccountId);
        builder.Property(e => e.AccountId)
            .ValueGeneratedOnAdd();
    }
}