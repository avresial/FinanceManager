using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class CashEntryConfiguration : IEntityTypeConfiguration<CashEntry>
{
    public void Configure(EntityTypeBuilder<CashEntry> builder)
    {
        // Physical names are pinned because they predate the CashEntry type name.
        builder.ToTable("CurrencyEntries");
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.CurrencyId).IsRequired();
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();

        builder.HasMany(e => e.Labels)
                .WithMany()
                .UsingEntity(
                    "CurrencyAccountEntryFinancialLabel",
                    right => right.HasOne(typeof(FinancialLabel)).WithMany().HasForeignKey("LabelsId"),
                    left => left.HasOne(typeof(CashEntry)).WithMany().HasForeignKey("CurrencyAccountEntryEntryId"),
                    join =>
                    {
                        join.ToTable("CurrencyAccountEntryFinancialLabel");
                        join.Property<long>("CurrencyAccountEntryEntryId").HasColumnName("CurrencyAccountEntryEntryId");
                        join.Property<int>("LabelsId").HasColumnName("LabelsId");
                    });

        builder.Property(e => e.ValueChange)
          .HasPrecision(18, 2);

        builder.Property(e => e.ContractorDetails)
            .HasMaxLength(140);

        builder.HasIndex(x => new { x.UserId, x.CurrencyId, x.PostingDate, x.EntryId })
            .HasDatabaseName("IX_CurrencyEntries_OwnerCurrencyDateId");
        builder.HasIndex(x => new { x.UserId, x.PostingDate, x.EntryId })
            .HasDatabaseName("IX_CurrencyEntries_OwnerDateId");

        // Ensure PostingDate is always returned as UTC
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );
        builder.Property(e => e.PostingDate).HasConversion(dateTimeConverter);
    }
}