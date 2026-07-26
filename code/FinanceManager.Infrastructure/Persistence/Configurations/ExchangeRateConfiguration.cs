using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.FromCurrency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.ToCurrency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(18, 8);
        builder.HasIndex(x => new { x.FromCurrency, x.ToCurrency, x.Date }).IsUnique();
    }
}