using FinanceManager.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        // Reset validation always looks a token up by its hash, so index it.
        builder.HasIndex(e => e.TokenHash)
            .IsUnique();

        builder.HasIndex(e => e.UserId);
    }
}