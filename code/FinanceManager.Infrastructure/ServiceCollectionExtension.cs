using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Insights.Generation;
using FinanceManager.Application.Labels.Setter;
using FinanceManager.Application.Labels.Suggestions;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Administration.Monitoring;
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
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Shared.Ai.Repositories;
using FinanceManager.Domain.Shared.Charting;
using FinanceManager.Domain.Shared.ExternalServices.Repositories;
using FinanceManager.Domain.Shared.Maintenance.Repositories;
using FinanceManager.Infrastructure.Features.Administration.Repositories;
using FinanceManager.Infrastructure.Features.Administration.Services;
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
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Shared.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureApi(this IServiceCollection services)
    {
        services.AddSingleton<IExternalServiceConfigService, ExternalServiceConfigService>();
        services.AddHttpClient<IAlphaVantageClient, AlphaVantageClient>();
        services.AddHttpClient<EodhdClient>();
        // Daily-price fetches go through a fallback chain: Alpha Vantage first, then EODHD when the
        // primary is rate-limited, unentitled, or has no data. AlphaVantageClient also implements
        // IStockPriceSource, so reuse the same singleton-per-scope instance the typed client resolves.
        services.AddScoped<IStockPriceSource>(sp => new FallbackStockPriceSource(
            [
                (IStockPriceSource)sp.GetRequiredService<IAlphaVantageClient>(),
                sp.GetRequiredService<EodhdClient>()
            ],
            sp.GetRequiredService<ILogger<FallbackStockPriceSource>>()));
        services.AddHttpClient<OpenFigiClient>();
        services.AddScoped<IOpenFigiClient>(sp => sp.GetRequiredService<OpenFigiClient>());
        services.AddHttpClient<ICurrencyExchangeRateProvider, FawazAhmedCurrencyApiClient>();
        services.AddHttpClient<IFxDailySource, AlphaVantageFxClient>();

        services.AddAI();

        services
                .AddScoped<IAssetRepository, AssetRepository>()
                .AddScoped<IAssetListingRepository, AssetListingRepository>()
                .AddScoped<IMarketDataSymbolRepository, MarketDataSymbolRepository>()
                .AddScoped<IInvestmentTransactionRepository, InvestmentTransactionRepository>()
                .AddScoped<IPriceQuoteRepository, PriceQuoteRepository>()
                .AddScoped<IFinancialAccountRepository, AccountRepository>()
                .AddScoped<IUserRepository, UserRepository>()
                .AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()
                .AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>()
                .AddScoped<IActiveUsersRepository, ActiveUsersRepository>()
                .AddScoped<IAccountRepository<InvestmentAccount>, InvestmentAccountRepository>()
                .AddScoped<IAccountRepository<BondAccount>, BondAccountRepository>()
                .AddScoped<ICurrencyAccountRepository<CurrencyAccount>, CurrencyAccountRepository>()
                .AddScoped<INewVisitsRepository, NewVisitsRepository>()
                .AddScoped<IFinancialInsightsRepository, FinancialInsightsRepository>()
                .AddScoped<IFinancialLabelsRepository, FinancialLabelsRepository>()
                .AddScoped<ICurrencyRepository, CurrencyRepository>()
                .AddScoped<IExchangeRateRepository, ExchangeRateRepository>()
                .AddScoped<IBondDetailsRepository, BondDetailsRepository>()
                .AddScoped<ICsvHeaderMappingRepository, CsvHeaderMappingRepository>()
                .AddScoped<IAiProviderConfigRepository, AiProviderConfigRepository>()
                .AddScoped<IExternalServiceConfigRepository, ExternalServiceConfigRepository>()
                .AddScoped<IMaintenanceKeyRepository, MaintenanceKeyRepository>()
                .AddScoped<ILogEntryRepository, LogEntryRepository>()

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

            void ConfigureRelational(DbContextOptionsBuilder options)
            {
                if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                    databaseProvider.Equals("Supabase", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseNpgsql(connectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
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
                    ConfigureRelational(options);
                });
            }
            else
            {
                services.AddDbContextPool<AppDbContext>((_, options) => ConfigureRelational(options));
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

    public static void ApplyMigrations(this IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (!dbContext.Database.IsRelational()) return;

        var pendingMigrations = dbContext.Database.GetPendingMigrations();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying pending migrations...");
            dbContext.Database.Migrate();
            logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations found.");
        }
    }
    public static T GetOptions<T>(this IConfiguration configuration, string sectionName) where T : class, new()
    {
        var section = configuration.GetSection(sectionName) ?? throw new ArgumentException($"Configuration section '{sectionName}' not found.");
        var options = new T();
        section.Bind(options);
        return options;
    }
}