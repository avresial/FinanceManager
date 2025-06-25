using FinanceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class DuplicateEntryConfiguration : IEntityTypeConfiguration<DuplicateEntry>
{
    public void Configure(EntityTypeBuilder<DuplicateEntry> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}