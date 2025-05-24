using FinanceManager.Domain.Entities.Accounts.Entries;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class BankAccountEntryContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<BankAccountEntry>(f =>
        {
            f.HasKey(e => e.EntryId);
            f.Property(e => e.EntryId)
                .ValueGeneratedOnAdd();
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "BankAccountEntryDb");
    }

    public DbSet<BankAccountEntry> Entries { get; set; }
}
