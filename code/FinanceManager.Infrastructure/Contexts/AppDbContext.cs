using FinanceManager.Domain.Entities.Ai;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.ExternalServices;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Entities.Logging;
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
    public DbSet<RefreshToken> RefreshTokens { get; set; } = default!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = default!;
    public DbSet<Currency> Currencies { get; set; } = default!;
    public DbSet<StockDetails> StockDetails { get; set; } = default!;
    public DbSet<FinancialAccountBaseDto> Accounts { get; set; } = default!;
    public DbSet<CurrencyAccountEntry> CurrencyEntries { get; set; } = default!;
    public DbSet<StockAccountEntry> StockEntries { get; set; } = default!;
    public DbSet<BondAccountEntry> BondEntries { get; set; } = default!;
    public DbSet<StockPriceDto> StockPrices { get; set; } = default!;
    public DbSet<NewVisits> NewVisits { get; set; } = default!;
    public DbSet<FinancialInsight> FinancialInsights { get; set; } = default!;
    public DbSet<FinancialLabel> FinancialLabels { get; set; } = default!;
    public DbSet<FinancialLabelClassification> FinancialLabelClassifications { get; set; } = default!;
    public DbSet<BondDetails> Bonds { get; set; } = default!;
    public DbSet<CsvHeaderMapping> CsvHeaderMappings { get; set; } = default!;
    public DbSet<AiProviderConfiguration> AiProviderConfigurations { get; set; } = default!;
    public DbSet<AiFallbackEntry> AiFallbackEntries { get; set; } = default!;
    public DbSet<AiProviderModel> AiProviderModels { get; set; } = default!;
    public DbSet<LogEntry> LogEntries { get; set; } = default!;
    public DbSet<ExternalServiceConfiguration> ExternalServiceConfigurations { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ActiveUserConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialAccountBaseDtoConfiguration());
        modelBuilder.ApplyConfiguration(new CurrencyAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CurrencyConfiguration());
        modelBuilder.ApplyConfiguration(new BondAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new NewVisitsConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialInsightConfiguration());
        modelBuilder.ApplyConfiguration(new StockAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new StockDetailsConfiguration());
        modelBuilder.ApplyConfiguration(new StockPriceDtoConfiguration());
        modelBuilder.ApplyConfiguration(new UserDtoConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetTokenConfiguration());
        modelBuilder.ApplyConfiguration(new BondDetailsConfiguration());
        modelBuilder.ApplyConfiguration(new BondCalculationMethodConfiguration());
        modelBuilder.ApplyConfiguration(new CsvHeaderMappingConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialLabelConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialLabelClassificationConfiguration());
        modelBuilder.ApplyConfiguration(new AiProviderConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new AiFallbackEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AiProviderModelConfiguration());
        modelBuilder.ApplyConfiguration(new LogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalServiceConfigurationConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}