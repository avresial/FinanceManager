using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Insights.Generation;
using FinanceManager.Application.Labels.Setter;
using FinanceManager.Application.Labels.Suggestions;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Application.Shared.Persistence;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Alerts.Repositories;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.GiftCodes;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Shared.Ai.Repositories;
using FinanceManager.Domain.Shared.Charting;
using FinanceManager.Domain.Shared.ExternalServices.Repositories;
using FinanceManager.Domain.Shared.Maintenance.Repositories;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Infrastructure.Features.Administration.Repositories;
using FinanceManager.Infrastructure.Features.Administration.Services;
using FinanceManager.Infrastructure.Features.Alerts.Repositories;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using FinanceManager.Infrastructure.Features.Assets.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Features.Identity.Repositories;
using FinanceManager.Infrastructure.Features.Insights.Repositories;
using FinanceManager.Infrastructure.Features.Labels.Repositories;
using FinanceManager.Infrastructure.Features.Mcp.OAuth;
using FinanceManager.Infrastructure.Features.MoneyFlow.Providers;
using FinanceManager.Infrastructure.Features.TransactionRules.Repositories;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Shared.Ai;
using FinanceManager.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureApi(this IServiceCollection services)
    {
        services.AddSingleton<IExternalServiceConfigService, ExternalServiceConfigService>();
        // Quota-counted requests must not acquire hidden retry attempts from ServiceDefaults.
#pragma warning disable EXTEXP0001 // Framework opt-out API is experimental; scoped to provider registration.
        services.AddSingleton<AlphaVantageRequestBudget>();
        services.AddHttpClient<IAlphaVantageClient, AlphaVantageClient>(client => client.Timeout = TimeSpan.FromSeconds(30)).RemoveAllResilienceHandlers();
        services.AddHttpClient<IMarketDataSymbolResolver, AlphaVantageSymbolResolver>(client => client.Timeout = TimeSpan.FromSeconds(30)).RemoveAllResilienceHandlers();
        services.AddHttpClient<IMarketDataSymbolResolver, TwelveDataSymbolResolver>(client => client.Timeout = TimeSpan.FromSeconds(30)).RemoveAllResilienceHandlers();
        services.AddHttpClient<IMarketDataSymbolResolver, EodhdSymbolResolver>(client => client.Timeout = TimeSpan.FromSeconds(30)).RemoveAllResilienceHandlers();
        services.AddHttpClient<EodhdClient>();
        services.AddSingleton<TwelveDataCreditBudget>();
        services.AddHttpClient<TwelveDataClient>((sp, client) =>
            client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<TwelveDataOptions>>().Value.RequestTimeoutSeconds)).RemoveAllResilienceHandlers();
        // Daily-price fetches use each provider's configured priority and provider-specific symbol.
        services.AddScoped<IStockPriceSource>(sp => new FallbackStockPriceSource(
            [
                (IStockPriceSource)sp.GetRequiredService<IAlphaVantageClient>(),
                sp.GetRequiredService<TwelveDataClient>(),
                sp.GetRequiredService<EodhdClient>()
            ],
            sp.GetRequiredService<ILogger<FallbackStockPriceSource>>()));
        services.AddHttpClient<OpenFigiClient>();
        services.AddScoped<IOpenFigiClient>(sp => sp.GetRequiredService<OpenFigiClient>());
        // Currency rate resolution runs as an ordered fallback chain (each provider reports NotFound for
        // pairs it can't serve): NBP is preferred for PLN pairs, ECB for EUR pairs and non-EUR cross
        // rates, then the general jsDelivr currency API. Both official sources are keyless.
        services.AddHttpClient<ICurrencyExchangeRateProvider, NbpCurrencyExchangeRateProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<ICurrencyExchangeRateProvider, EcbCurrencyExchangeRateProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<ICurrencyExchangeRateProvider, FawazAhmedCurrencyApiClient>();
        services.AddHttpClient<AlphaVantageFxClient>(client => client.Timeout = TimeSpan.FromSeconds(30)).RemoveAllResilienceHandlers();
        services.AddScoped<IFxDailySource>(sp => new FallbackFxDailySource(
        [
            sp.GetRequiredService<AlphaVantageFxClient>(),
            sp.GetRequiredService<TwelveDataClient>()
        ],
            sp.GetRequiredService<ILogger<FallbackFxDailySource>>()));
#pragma warning restore EXTEXP0001
        services.AddHttpClient<IInflationIndexProvider, EurostatInflationIndexProvider>(client =>
            client.BaseAddress = new Uri("https://ec.europa.eu/eurostat/api/dissemination/statistics/1.0/data/"));

        services.AddAI();

        services
                .AddScoped<IAssetRepository, AssetRepository>()
                .AddScoped<IAssetListingRepository, AssetListingRepository>()
                .AddScoped<IMarketDataSymbolRepository, MarketDataSymbolRepository>()
                .AddScoped<IInvestmentTransactionRepository, InvestmentTransactionRepository>()
                .AddScoped<IDividendPaymentRepository, DividendPaymentRepository>()
                .AddScoped<IAtomicOperation, EfAtomicOperation>()
                .AddScoped<IPriceQuoteRepository, PriceQuoteRepository>()
                .AddScoped<IFinancialAccountRepository, AccountRepository>()
                .AddScoped<IUserRepository, UserRepository>()
                .AddScoped<IGiftCodeRepository, GiftCodeRepository>()
                .AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()
                .AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>()
                .AddScoped<IActiveUsersRepository, ActiveUsersRepository>()
                .AddScoped<IAccountRepository<InvestmentAccount>, InvestmentAccountRepository>()
                .AddScoped<IAccountRepository<BondAccount>, BondAccountRepository>()
                .AddScoped<ICurrencyAccountRepository<CurrencyAccount>, CurrencyAccountRepository>()
                .AddScoped<INewVisitsRepository, NewVisitsRepository>()
                .AddScoped<IFinancialInsightsRepository, FinancialInsightsRepository>()
                .AddScoped<IFinancialLabelsRepository, FinancialLabelsRepository>()
                .AddScoped<IRecurringSubscriptionRepository, RecurringSubscriptionRepository>()
                .AddScoped<IFinancialAlertRepository, FinancialAlertRepository>()
                .AddScoped<ICurrencyRepository, CurrencyRepository>()
                .AddScoped<IExchangeRateRepository, ExchangeRateRepository>()
                .AddScoped<IBondDetailsRepository, BondDetailsRepository>()
                .AddScoped<ICsvHeaderMappingRepository, CsvHeaderMappingRepository>()
                .AddScoped<IAiProviderConfigRepository, AiProviderConfigRepository>()
                .AddScoped<IExternalServiceConfigRepository, ExternalServiceConfigRepository>()
                .AddScoped<IMaintenanceKeyRepository, MaintenanceKeyRepository>()
                .AddScoped<ILogEntryRepository, LogEntryRepository>()
                .AddScoped<ITransactionRuleRepository, TransactionRuleRepository>()

                .AddSingleton<IInsightsPromptProvider, InsightsPromptProvider>()
                .AddSingleton<ILabelSetterPromptProvider, LabelSetterPromptProvider>()
                .AddSingleton<ILabelSuggestionPromptProvider, LabelSuggestionPromptProvider>()

                .AddHostedService<DatabaseInitializer>()
                ;

        AddCachedEntryRepositories(services);

        return services;
    }

    // Account entry repositories are registered as inner concrete services wrapped by HybridCache decorators
    // (CachedAccountEntryRepository<T>) that cache point-reads and month-bucket range reads, and bust the
    // owner's cache on every write. The owner resolver translates accountId → userId for per-user tags.
    // See issues #455 (point-read cache) and #456 (range/bucket cache).
    private static void AddCachedEntryRepositories(IServiceCollection services)
    {
        services.AddSingleton(new EntryRangeCacheOptions());
        services.AddScoped<IAccountUserResolver, AccountUserResolver>();

        services.AddScoped<CurrencyEntryRepository>();
        services.AddScoped<IAccountEntryRepository<CurrencyAccountEntry>>(sp =>
            new CachedAccountEntryRepository<CurrencyAccountEntry>(
                sp.GetRequiredService<CurrencyEntryRepository>(),
                sp.GetRequiredService<IAccountUserResolver>(),
                sp.GetRequiredService<ICacheInvalidator>(),
                sp.GetRequiredService<HybridCache>(),
                sp.GetRequiredService<EntryRangeCacheOptions>()));

        services.AddScoped<BondEntryRepository>();
        services.AddScoped<CachedBondEntryRepository>(sp =>
            new CachedBondEntryRepository(
                sp.GetRequiredService<BondEntryRepository>(),
                sp.GetRequiredService<IAccountUserResolver>(),
                sp.GetRequiredService<ICacheInvalidator>(),
                sp.GetRequiredService<HybridCache>(),
                sp.GetRequiredService<EntryRangeCacheOptions>()));
        services.AddScoped<IBondAccountEntryRepository<BondAccountEntry>>(sp => sp.GetRequiredService<CachedBondEntryRepository>());
        services.AddScoped<IAccountEntryRepository<BondAccountEntry>>(sp => sp.GetRequiredService<CachedBondEntryRepository>());
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.AddSingleton<PostgresConnectionOpenRetryInterceptor>();
        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>());
        services.AddScoped<McpOAuthConfigurationReconciler>();
        services.AddScoped<McpOAuthGrantRevoker>();

        // The cleanup service builds standalone AppDbContexts to drop expired guest sandboxes; without a shared
        // root, EF Core's InMemory provider uses a per-internal-provider singleton, so the standalone context
        // would target a different store than the DI-resolved one and EnsureDeleted() would silently miss.
        services.AddSingleton<InMemoryDatabaseRoot>();

        if (configuration.GetValue("UseInMemoryDatabase", false))
        {
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                var dbName = GuestDatabaseNaming.ResolveDatabaseName(sp, defaultName: "Db");
                options.UseInMemoryDatabase(databaseName: dbName, sp.GetRequiredService<InMemoryDatabaseRoot>())
                    .UseOpenIddict();
            });
        }
        else
        {
            var appHostConnectionString = configuration.GetConnectionString("FinanceManagerDb");
            var developmentConnectionString = configuration.GetConnectionString("DefaultConnection");
            var fallbackConnectionString = configuration.GetValue<string>("FINANCE_MANAGER_DB_KEY");

            var connectionString = appHostConnectionString
                ?? developmentConnectionString
                ?? fallbackConnectionString;

            var databaseProvider = InferDatabaseProvider(connectionString,
                configuration.GetValue("DatabaseProvider", "SqlServer") ?? "SqlServer");

            void ConfigureRelational(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
            {
                if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                    databaseProvider.Equals("Supabase", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseNpgsql(connectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
                    options.AddInterceptors(serviceProvider.GetRequiredService<PostgresConnectionOpenRetryInterceptor>());
                }
                else
                {
                    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
                }

                options.UseOpenIddict();
            }

            // Guest sandbox resolves a per-user in-memory database name at request time; that
            // per-request variability is incompatible with DbContext pooling.
            var enableGuestSessionSandbox = configuration.GetValue("EnableGuestSessionSandbox", false);
            if (enableGuestSessionSandbox)
            {
                services.AddDbContext<AppDbContext>((sp, options) =>
                {
                    var guestDbName = GuestDatabaseNaming.TryGetGuestDatabaseName(sp);
                    if (guestDbName is not null)
                    {
                        options.UseInMemoryDatabase(databaseName: guestDbName, sp.GetRequiredService<InMemoryDatabaseRoot>());
                        options.UseOpenIddict();
                        return;
                    }
                    ConfigureRelational(sp, options);
                });
            }
            else
            {
                services.AddDbContextPool<AppDbContext>((sp, options) => ConfigureRelational(sp, options));
            }
        }

        return services;
    }

    private static string InferDatabaseProvider(string? connectionString, string configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return configuredProvider;

        if (connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase))
        {
            return "PostgreSQL";
        }

        if (connectionString.Contains("Trusted_Connection=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return "SqlServer";
        }

        return configuredProvider;
    }

}