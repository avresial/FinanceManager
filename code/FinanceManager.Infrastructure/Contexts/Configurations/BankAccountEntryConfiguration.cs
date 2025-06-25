using FinanceManager.Domain.Entities.Accounts.Entries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class BankAccountEntryConfiguration : IEntityTypeConfiguration<BankAccountEntry>
{
    public void Configure(EntityTypeBuilder<BankAccountEntry> builder)
    {
        builder.HasKey(e => e.EntryId);
        builder.Property(e => e.EntryId)
            .ValueGeneratedOnAdd();
    }
}