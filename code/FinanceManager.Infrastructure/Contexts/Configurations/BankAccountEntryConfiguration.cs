using FinanceManager.Domain.Entities.Cash;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class BankAccountEntryConfiguration : IEntityTypeConfiguration<BankAccountEntry>
{
    public void Configure(EntityTypeBuilder<BankAccountEntry> builder)
    {
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();

        builder.HasMany(e => e.Labels)
            .WithMany(e => e.Entries);
        //.HasForeignKey(l => l.Id)
        //.OnDelete(DeleteBehavior.SetNull);

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