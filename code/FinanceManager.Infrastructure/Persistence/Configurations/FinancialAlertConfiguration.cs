using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Identity.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

internal sealed class FinancialAlertConfiguration : IEntityTypeConfiguration<FinancialAlert>
{
    public void Configure(EntityTypeBuilder<FinancialAlert> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.LabelName)
            .HasMaxLength(200);

        builder.Property(x => x.MerchantName)
            .HasMaxLength(200);

        builder.Property(x => x.LastTriggeredConditionFingerprint)
            .HasMaxLength(1000);

        builder.Property(x => x.Threshold)
            .HasPrecision(18, 2);

        // TimeSpan is intentionally kept as the provider-native duration/time value. Both configured
        // relational providers and the in-memory test provider support nullable TimeSpan properties.
        builder.HasOne<UserDto>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.IsEnabled });
        builder.HasIndex(x => new { x.UserId, x.AlertType });
    }
}