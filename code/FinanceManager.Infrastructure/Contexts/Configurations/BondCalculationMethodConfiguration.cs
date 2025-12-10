using FinanceManager.Domain.Entities.Bonds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class BondCalculationMethodConfiguration : IEntityTypeConfiguration<BondCalculationMethod>
{
    public void Configure(EntityTypeBuilder<BondCalculationMethod> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.DateValue)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Rate)
            .HasPrecision(18, 6)
            .IsRequired();

        // Configure relationship and shadow FK
        builder.HasOne(e => e.BondDetails)
            .WithMany(b => b.CalculationMethods)
            .HasForeignKey("BondDetailsId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ensure the shadow FK is required
        builder.Property<int>("BondDetailsId").IsRequired();
    }
}