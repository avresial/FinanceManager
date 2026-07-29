using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Shared.Ai.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Domain.Shared.Maintenance.Entities;
using FinanceManager.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ActiveUser> ActiveUsers { get; set; } = default!;
    public DbSet<UserDto> Users { get; set; } = default!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = default!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = default!;
    public DbSet<Currency> Currencies { get; set; } = default!;
    public DbSet<ExchangeRate> ExchangeRates { get; set; } = default!;
    public DbSet<FinancialAccountBaseDto> Accounts { get; set; } = default!;
    public DbSet<CurrencyAccountEntry> CurrencyEntries { get; set; } = default!;
    public DbSet<BondAccountEntry> BondEntries { get; set; } = default!;
    public DbSet<Asset> Assets { get; set; } = default!;
    public DbSet<AssetListing> AssetListings { get; set; } = default!;
    public DbSet<MarketDataSymbol> MarketDataSymbols { get; set; } = default!;
    public DbSet<AssetIdentifier> AssetIdentifiers { get; set; } = default!;
    public DbSet<InvestmentTransaction> InvestmentTransactions { get; set; } = default!;
    public DbSet<PriceQuote> PriceQuotes { get; set; } = default!;
    public DbSet<NewVisits> NewVisits { get; set; } = default!;
    public DbSet<FinancialInsight> FinancialInsights { get; set; } = default!;
    public DbSet<FinancialLabel> FinancialLabels { get; set; } = default!;
    public DbSet<FinancialLabelClassification> FinancialLabelClassifications { get; set; } = default!;
    public DbSet<RecurringSubscription> RecurringSubscriptions { get; set; } = default!;
    public DbSet<BondDetails> Bonds { get; set; } = default!;
    public DbSet<CsvHeaderMapping> CsvHeaderMappings { get; set; } = default!;
    public DbSet<AiProviderConfiguration> AiProviderConfigurations { get; set; } = default!;
    public DbSet<AiFallbackEntry> AiFallbackEntries { get; set; } = default!;
    public DbSet<AiProviderModel> AiProviderModels { get; set; } = default!;
    public DbSet<LogEntry> LogEntries { get; set; } = default!;
    public DbSet<ExternalServiceConfiguration> ExternalServiceConfigurations { get; set; } = default!;
    public DbSet<MaintenanceApiKey> MaintenanceApiKeys { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ActiveUserConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialAccountBaseDtoConfiguration());
        modelBuilder.ApplyConfiguration(new CurrencyAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CurrencyConfiguration());
        modelBuilder.ApplyConfiguration(new ExchangeRateConfiguration());
        modelBuilder.ApplyConfiguration(new BondAccountEntryConfiguration());
        modelBuilder.ApplyConfiguration(new NewVisitsConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialInsightConfiguration());
        modelBuilder.ApplyConfiguration(new AssetConfiguration());
        modelBuilder.ApplyConfiguration(new AssetListingConfiguration());
        modelBuilder.ApplyConfiguration(new MarketDataSymbolConfiguration());
        modelBuilder.ApplyConfiguration(new AssetIdentifierConfiguration());
        modelBuilder.ApplyConfiguration(new InvestmentTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new PriceQuoteConfiguration());
        modelBuilder.ApplyConfiguration(new UserDtoConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetTokenConfiguration());
        modelBuilder.ApplyConfiguration(new BondDetailsConfiguration());
        modelBuilder.ApplyConfiguration(new BondCalculationMethodConfiguration());
        modelBuilder.ApplyConfiguration(new CsvHeaderMappingConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialLabelConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialLabelClassificationConfiguration());
        modelBuilder.ApplyConfiguration(new RecurringSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new AiProviderConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new AiFallbackEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AiProviderModelConfiguration());
        modelBuilder.ApplyConfiguration(new LogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalServiceConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new MaintenanceApiKeyConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}