using FinanceManager.Domain.Entities.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class StockAccountContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockAccount>()
                    .Property(f => f.AccountId)
                    .ValueGeneratedOnAdd();

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "StockAccountDb");
    }

    public DbSet<StockAccount> StockAccounts { get; set; }
}
