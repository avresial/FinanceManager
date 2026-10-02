using FinanceManager.Domain.Identity.GiftCodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class GiftCodeConfiguration : IEntityTypeConfiguration<GiftCode>
{
    public void Configure(EntityTypeBuilder<GiftCode> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.CodeHash).IsUnique();
        builder.Property(x => x.CodeSuffix).HasMaxLength(4).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(200);
        // Audit actor IDs intentionally survive deletion of the corresponding account.
    }
}