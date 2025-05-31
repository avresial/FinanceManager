using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class StockAccountContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinancialAccountBaseDto>(f =>
        {
            f.HasKey(e => e.AccountId);
            f.Property(e => e.AccountId)
                .ValueGeneratedOnAdd();
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "StockAccountDb");
    }

    public DbSet<FinancialAccountBaseDto> StockAccounts { get; set; }
}
