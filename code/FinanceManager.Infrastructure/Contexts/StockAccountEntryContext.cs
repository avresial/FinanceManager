using FinanceManager.Domain.Entities.Accounts.Entries;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class StockAccountEntryContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockAccountEntry>(f =>
        {
            f.HasKey(e => e.EntryId);
            f.Property(e => e.EntryId)
                .ValueGeneratedOnAdd();
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "StockAccountEntryDb");
    }

    public DbSet<StockAccountEntry> Entries { get; set; }
}

