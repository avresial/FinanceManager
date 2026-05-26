using FinanceManager.Domain.Entities.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class LogEntryConfiguration : IEntityTypeConfiguration<LogEntry>
{
    public void Configure(EntityTypeBuilder<LogEntry> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Category)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Message)
            .HasMaxLength(4096)
            .IsRequired();

        builder.Property(e => e.EventName)
            .HasMaxLength(256);

        builder.HasIndex(e => e.TimestampUtc);
        builder.HasIndex(e => new { e.Level, e.TimestampUtc });
    }
}