using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Infrastructure.Contexts;
public class AppDbContext(IConfiguration configuration) : DbContext
{
    private readonly IConfiguration _configuration = configuration;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActiveUser>(f =>
        {
            f.Property(e => e.Id)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<FinancialAccountBaseDto>(f =>
        {
            f.HasKey(e => e.AccountId);
            f.Property(e => e.AccountId)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<BankAccountEntry>(f =>
        {
            f.HasKey(e => e.EntryId);
            f.Property(e => e.EntryId)
                .ValueGeneratedOnAdd();
        });
        modelBuilder.Entity<NewVisits>(f =>
        {
            f.Property(e => e.Id)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StockAccountEntry>(f =>
        {
            f.HasKey(e => e.EntryId);
            f.Property(e => e.EntryId)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StockPriceDto>(f =>
        {
            f.Property(e => e.Id)
                .ValueGeneratedOnAdd();
        });


        modelBuilder.Entity<UserDto>(f =>
        {
            f.Property(e => e.Id)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<DuplicateEntry>(f =>
        {
            f.Property(e => e.Id)
                .ValueGeneratedOnAdd();
        });


        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "Db");

        //var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        //optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
    }

    public DbSet<ActiveUser> ActiveUsers { get; set; }
    public DbSet<UserDto> Users { get; set; }

    public DbSet<FinancialAccountBaseDto> BankAccounts { get; set; }
    public DbSet<FinancialAccountBaseDto> StockAccounts { get; set; }
    public DbSet<BankAccountEntry> BankEntries { get; set; }
    public DbSet<StockAccountEntry> StockEntries { get; set; }
    public DbSet<StockPriceDto> StockPrices { get; set; }
    public DbSet<NewVisits> NewVisits { get; set; }
    public DbSet<DuplicateEntry> DuplicateEntries { get; set; }

}
