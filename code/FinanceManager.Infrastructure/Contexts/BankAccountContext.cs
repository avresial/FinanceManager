using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class BankAccountContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BankAccountInformationsDto>(f =>
        {
            f.HasKey(e => e.AccountId);
            f.Property(e => e.AccountId)
                .ValueGeneratedOnAdd();
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "BankAccountDb");
    }

    public DbSet<BankAccountInformationsDto> BankAccounts { get; set; }
}