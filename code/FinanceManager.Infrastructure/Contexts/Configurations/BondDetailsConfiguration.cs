using FinanceManager.Domain.Entities.Bonds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class BondDetailsConfiguration : IEntityTypeConfiguration<BondDetails>
{
    public void Configure(EntityTypeBuilder<BondDetails> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Issuer)
            .IsRequired()
            .HasMaxLength(200);

        // Store enum as int
        builder.Property(e => e.Type).HasConversion<int>();

        // Relationship: BondDetails 1..* BondCalculationMethod
        builder.HasMany(e => e.CalculationMethods)
            .WithOne(m => m.BondDetails)
            .HasForeignKey("BondDetailsId")
            .OnDelete(DeleteBehavior.Cascade);

        // Use the backing field for the read-only collection
        builder.Navigation(e => e.CalculationMethods)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}