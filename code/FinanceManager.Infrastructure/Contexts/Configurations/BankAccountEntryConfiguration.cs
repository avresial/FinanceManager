using FinanceManager.Domain.Entities.FinancialAccounts.Currency;
using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class BankAccountEntryConfiguration : IEntityTypeConfiguration<CurrencyAccountEntry>
{
    public void Configure(EntityTypeBuilder<CurrencyAccountEntry> builder)
    {
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();

        builder.HasMany(e => e.Labels)
                .WithMany()
                .UsingEntity(j => j.ToTable("BankAccountEntryFinancialLabel"));

        builder.Property(e => e.Value)
            .HasPrecision(18, 2);

        builder.Property(e => e.ValueChange)
          .HasPrecision(18, 2);

        // Ensure PostingDate is always returned as UTC
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );
        builder.Property(e => e.PostingDate).HasConversion(dateTimeConverter);
    }
}