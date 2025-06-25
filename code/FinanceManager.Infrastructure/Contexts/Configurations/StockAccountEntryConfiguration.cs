using FinanceManager.Domain.Entities.Accounts.Entries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class StockAccountEntryConfiguration : IEntityTypeConfiguration<StockAccountEntry>
{
    public void Configure(EntityTypeBuilder<StockAccountEntry> builder)
    {
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();
    }
}