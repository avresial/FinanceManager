using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

internal sealed class TransactionRuleDefinitionConfiguration : IEntityTypeConfiguration<TransactionRuleDefinition>
{
    public void Configure(EntityTypeBuilder<TransactionRuleDefinition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ConditionsJson).IsRequired();
        builder.Property(x => x.ActionsJson).IsRequired();
        builder.HasOne<UserDto>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UserId, x.Order }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsEnabled });
    }
}