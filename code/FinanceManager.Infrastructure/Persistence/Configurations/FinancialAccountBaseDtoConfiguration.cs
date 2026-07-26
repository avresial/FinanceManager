using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class FinancialAccountBaseDtoConfiguration : IEntityTypeConfiguration<FinancialAccountBaseDto>
{
    public void Configure(EntityTypeBuilder<FinancialAccountBaseDto> builder)
    {
        builder.HasKey(e => e.AccountId);
        builder.Property(e => e.AccountId)
            .ValueGeneratedOnAdd();

        builder.HasIndex(e => new { e.AccountType, e.UserId });
    }
}