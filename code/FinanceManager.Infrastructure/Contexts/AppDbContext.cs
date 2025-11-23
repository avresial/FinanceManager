using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Entities.Shared.Accounts; 
using FinanceManager.Infrastructure.Contexts.Configurations;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.Infrastructure.Contexts;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ActiveUser> ActiveUsers { get; set; }
    public DbSet<UserDto> Users { get; set; }
    public DbSet<FinancialAccountBaseDto> Accounts { get; set; }
    public DbSet<BankAccountEntry> BankEntries { get; set; }
    public DbSet<StockAccountEntry> StockEntries { get; set; }
    public DbSet<StockPriceDto> StockPrices { get; set; }
    public DbSet<NewVisits> NewVisits { get; set; }
    public DbSet<FinancialLabel> FinancialLabels { get; set; }
    public DbSet<BondDetails> Bonds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ActiveUserConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialAccountBaseDtoConfiguration());
        modelBuilder.ApplyConfiguration(new BankAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new NewVisitsConfiguration());
        modelBuilder.ApplyConfiguration(new StockAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new StockPriceDtoConfiguration());
        modelBuilder.ApplyConfiguration(new UserDtoConfiguration());
        modelBuilder.ApplyConfiguration(new BondDetailsConfiguration());
        modelBuilder.ApplyConfiguration(new BondCalculationMethodConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}