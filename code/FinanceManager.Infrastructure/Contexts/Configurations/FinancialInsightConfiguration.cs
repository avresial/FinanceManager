using FinanceManager.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class FinancialInsightConfiguration : IEntityTypeConfiguration<FinancialInsight>
{
    public void Configure(EntityTypeBuilder<FinancialInsight> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Title)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.Message)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(e => e.Tags)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(e => new { e.UserId, e.CreatedAt });
    }
}