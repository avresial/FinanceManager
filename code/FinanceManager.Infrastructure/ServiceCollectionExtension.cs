using FinanceManager.Application.Options;
using FinanceManager.Application.Services.Ai;
using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Providers;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Repositories.Account;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using FinanceManager.Infrastructure.Services;
using FinanceManager.Infrastructure.Services.Ai;
using FinanceManager.Infrastructure.Services.Stocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;
using System.ClientModel;

namespace FinanceManager.Infrastructure;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureApi(this IServiceCollection services)
    {
        services.AddHttpClient<IAlphaVantageClient, AlphaVantageClient>();

        services.AddScoped<IChatClient>(static sp =>
        {
            var providerOptions = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
            return (providerOptions.Provider ?? "OpenRouter").Trim() switch
            {
                { } p when p.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
                    => CreateOllamaChatClient(sp),
                { } p when p.Equals("GitHubModels", StringComparison.OrdinalIgnoreCase)
                    || p.Equals("GitHub", StringComparison.OrdinalIgnoreCase)
                    => CreateGitHubModelsChatClient(sp),
                _ => CreateOpenRouterChatClient(sp)
            };
        });

        services
                .AddScoped<IStockPriceRepository, StockPriceRepository>()
                .AddScoped<IStockDetailsRepository, StockDetailsRepository>()
                .AddScoped<IFinancialAccountRepository, AccountRepository>()
                .AddScoped<IUserRepository, UserRepository>()
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
                .AddScoped<IFinancialInsightsAiGenerator, FinancialInsightsAiGenerator>()
                .AddScoped<ILabelSetterAiService, LabelSetterAiService>()

                .AddSingleton<IInsightsPromptProvider, InsightsPromptProvider>()
                .AddSingleton<ILabelSetterPromptProvider, LabelSetterPromptProvider>()

                .AddHostedService<DatabaseInitializer>()
                ;

        return services;
    }

    private static IChatClient CreateOpenRouterChatClient(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<OpenRouterOptions>>().Value;
        var timeoutSeconds = options.RequestTimeoutSeconds > 0 ? options.RequestTimeoutSeconds : 180;
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.BaseUrl),
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        var openAIClient = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        return openAIClient.GetChatClient(options.Model).AsIChatClient();
    }

    private static IChatClient CreateGitHubModelsChatClient(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<GitHubModelsOptions>>().Value;
        var timeoutSeconds = options.RequestTimeoutSeconds > 0 ? options.RequestTimeoutSeconds : 180;
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.BaseUrl),
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        var openAIClient = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        return openAIClient.GetChatClient(options.Model).AsIChatClient();
    }

    private static IChatClient CreateOllamaChatClient(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
        return new OllamaApiClient(new Uri(options.BaseUrl), options.Model);
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfigurationManager configuration)
    {
        if (configuration.GetValue("UseInMemoryDatabase", false))
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: "Db"));
        }
        else
        {
            var databaseProvider = configuration.GetValue("DatabaseProvider", "SqlServer") ?? "SqlServer";

            services.AddDbContext<AppDbContext>(options =>
            {
                var connectionString = configuration.GetValue<string>("FINANCE_MANAGER_DB_KEY");
                if (configuration.GetSection("ConnectionStrings").Exists())
                    connectionString = configuration.GetSection("ConnectionStrings").GetValue<string>("FinanceManagerDb");

                if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                    databaseProvider.Equals("Supabase", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseNpgsql(connectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
                }
                else
                {
                    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
                }
            });
        }
        return services;
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