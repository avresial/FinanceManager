using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currency;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Infrastructure.Contexts.Configurations;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ActiveUser> ActiveUsers { get; set; } = default!;
    public DbSet<UserDto> Users { get; set; } = default!;
    public DbSet<FinancialAccountBaseDto> Accounts { get; set; } = default!;
    public DbSet<CurrencyAccountEntry> BankEntries { get; set; } = default!;
    public DbSet<StockAccountEntry> StockEntries { get; set; } = default!;
    public DbSet<BondAccountEntry> BondEntries { get; set; } = default!;
    public DbSet<StockPriceDto> StockPrices { get; set; } = default!;
    public DbSet<NewVisits> NewVisits { get; set; } = default!;
    public DbSet<FinancialLabel> FinancialLabels { get; set; } = default!;
    public DbSet<BondDetails> Bonds { get; set; } = default!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ActiveUserConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialAccountBaseDtoConfiguration());
        modelBuilder.ApplyConfiguration(new BankAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new BondAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new NewVisitsConfiguration());
        modelBuilder.ApplyConfiguration(new StockAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new StockPriceDtoConfiguration());
        modelBuilder.ApplyConfiguration(new UserDtoConfiguration());
        modelBuilder.ApplyConfiguration(new BondDetailsConfiguration());
        modelBuilder.ApplyConfiguration(new BondCalculationMethodConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}