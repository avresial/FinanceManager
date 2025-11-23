using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Infrastructure.Contexts.Configurations;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;
public class AppDbContext : DbContext
{
    public DbSet<ActiveUser> ActiveUsers { get; set; }
    public DbSet<UserDto> Users { get; set; }
    public DbSet<FinancialAccountBaseDto> Accounts { get; set; }
    public DbSet<BankAccountEntry> BankEntries { get; set; }
    public DbSet<StockAccountEntry> StockEntries { get; set; }
    public DbSet<StockPriceDto> StockPrices { get; set; }
    public DbSet<NewVisits> NewVisits { get; set; }
    public DbSet<FinancialLabel> FinancialLabels { get; set; }


    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ActiveUserConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialAccountBaseDtoConfiguration());
        modelBuilder.ApplyConfiguration(new BankAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new NewVisitsConfiguration());
        modelBuilder.ApplyConfiguration(new StockAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new StockPriceDtoConfiguration());
        modelBuilder.ApplyConfiguration(new UserDtoConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}