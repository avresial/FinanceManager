using FinanceManager.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.ReplacedByTokenHash)
            .HasMaxLength(128);

        // Refresh validation always looks a token up by its hash, so index it.
        builder.HasIndex(e => e.TokenHash)
            .IsUnique();

        builder.HasIndex(e => e.FamilyId);
    }
}