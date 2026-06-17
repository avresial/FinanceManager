using FinanceManager.Application.FinancialAccounts.Stock.Market;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Insights.Generation;
using FinanceManager.Application.Labels.Setter;
using FinanceManager.Application.Labels.Suggestions;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Guest;
using FinanceManager.Infrastructure.Providers;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Repositories.Account;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using FinanceManager.Infrastructure.Services;
using FinanceManager.Infrastructure.Services.Ai;
using FinanceManager.Infrastructure.Services.Currencies;
using FinanceManager.Infrastructure.Services.ExternalServices;
using FinanceManager.Infrastructure.Services.Stocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        services.AddHttpClient<OpenFigiClient>();
        services.AddScoped<IOpenFigiClient>(sp => sp.GetRequiredService<OpenFigiClient>());
        services.AddScoped<IIsinResolver, CachingIsinResolver>();
        services.AddScoped<IQuoteFactorResolver>(sp =>
            new QuoteFactorResolver(
                sp.GetRequiredService<ICurrencyExchangeRateProvider>(),
                sp.GetRequiredService<ILogger<QuoteFactorResolver>>()));
        services.AddScoped<IInstrumentResolver>(sp =>
            new InstrumentResolver(
                sp.GetRequiredService<IOpenFigiClient>(),
                sp.GetRequiredService<IAlphaVantageClient>(),
                sp.GetRequiredService<IStockDetailsRepository>(),
                sp.GetRequiredService<ICurrencyRepository>(),
                sp.GetRequiredService<IQuoteFactorResolver>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<InstrumentResolver>>()));
        services.AddHttpClient<ICurrencyExchangeRateProvider, FawazAhmedCurrencyApiClient>();

        services.AddAI();

        services
                .AddScoped<IDataBackfillService, DataBackfillService>()
                .AddScoped<IStockPriceRepository, StockPriceRepository>()
                .AddScoped<IStockDetailsRepository, StockDetailsRepository>()
                .AddScoped<IFinancialAccountRepository, AccountRepository>()
                .AddScoped<IUserRepository, UserRepository>()
                .AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()
                .AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>()
                .AddScoped<IActiveUsersRepository, ActiveUsersRepository>()
                .AddScoped<IAccountEntryRepository<CurrencyAccountEntry>, CurrencyEntryRepository>()
                .AddScoped<IAccountEntryRepository<BondAccountEntry>, BondEntryRepository>()
                .AddScoped<IBondAccountEntryRepository<BondAccountEntry>, BondEntryRepository>()
                .AddScoped<IStockAccountEntryRepository<StockAccountEntry>, StockEntryRepository>()
                .AddScoped<IAccountRepository<StockAccount>, StockAccountRepository>()
                .AddScoped<IAccountRepository<BondAccount>, BondAccountRepository>()
                .AddScoped<ICurrencyAccountRepository<CurrencyAccount>, CurrencyAccountRepository>()
                .AddScoped<INewVisitsRepository, NewVisitsRepository>()
                .AddScoped<IFinancialInsightsRepository, FinancialInsightsRepository>()
                .AddScoped<IFinancialLabelsRepository, FinancialLabelsRepository>()
                .AddScoped<ICurrencyRepository, CurrencyRepository>()
                .AddScoped<IBondDetailsRepository, BondDetailsRepository>()
                .AddScoped<ICsvHeaderMappingRepository, CsvHeaderMappingRepository>()
                .AddScoped<IInflationDataProvider, InMemoryInflationDataProvider>()
                .AddScoped<IAiProviderConfigRepository, AiProviderConfigRepository>()
                .AddScoped<IExternalServiceConfigRepository, ExternalServiceConfigRepository>()
                .AddScoped<ILogEntryRepository, LogEntryRepository>()

                .AddSingleton<IInsightsPromptProvider, InsightsPromptProvider>()
                .AddSingleton<ILabelSetterPromptProvider, LabelSetterPromptProvider>()
                .AddSingleton<ILabelSuggestionPromptProvider, LabelSuggestionPromptProvider>()

                .AddHostedService<DatabaseInitializer>()
                ;

        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfigurationManager configuration)
    {
        // The cleanup service builds standalone AppDbContexts to drop expired guest sandboxes; without a shared
        // root, EF Core's InMemory provider uses a per-internal-provider singleton, so the standalone context
        // would target a different store than the DI-resolved one and EnsureDeleted() would silently miss.
        services.AddSingleton<InMemoryDatabaseRoot>();

        if (configuration.GetValue("UseInMemoryDatabase", false))
        {
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                var dbName = GuestDatabaseNaming.ResolveDatabaseName(sp, defaultName: "Db");
                options.UseInMemoryDatabase(databaseName: dbName, sp.GetRequiredService<InMemoryDatabaseRoot>());
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