using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
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
        builder.HasOne<FinancialAccountBaseDto>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UserId, x.AccountId, x.PaymentDate, x.Id });
        builder.HasIndex(x => x.AssetListingId);
    }
}