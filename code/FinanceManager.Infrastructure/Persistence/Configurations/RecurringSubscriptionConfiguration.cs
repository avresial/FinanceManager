using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

internal class RecurringSubscriptionConfiguration : IEntityTypeConfiguration<RecurringSubscription>
{
    public void Configure(EntityTypeBuilder<RecurringSubscription> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MerchantKey).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(300);
        builder.HasIndex(x => new { x.UserId, x.MerchantKey }).IsUnique();
        builder.HasOne<UserDto>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}