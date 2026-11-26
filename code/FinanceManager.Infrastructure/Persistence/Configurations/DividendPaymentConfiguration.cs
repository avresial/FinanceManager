using FinanceManager.Domain.Portfolio.Dividends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class DividendPaymentConfiguration : IEntityTypeConfiguration<DividendPayment>
{
    public void Configure(EntityTypeBuilder<DividendPayment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 8);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(512);
        builder.HasOne(x => x.AssetListing).WithMany().HasForeignKey(x => x.AssetListingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.PaymentDate, x.Id })
            .HasDatabaseName("IX_DividendPayments_OwnerPaymentDateId");
        builder.HasIndex(x => x.AssetListingId);
    }
}