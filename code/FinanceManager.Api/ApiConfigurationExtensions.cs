using FinanceManager.Api.Features.Administration.Logging;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Api.Features.Identity.Guest;
using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Api.Features.Insights.Services;
using FinanceManager.Api.Features.Labels.Services;
using FinanceManager.Api.Features.Maintenance.Services;
using FinanceManager.Api.Features.Mcp.OAuth;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.Features.Mcp.OAuth;
using Microsoft.Extensions.Options;

namespace FinanceManager.Api;

/// <summary>
/// Groups the API host's DI wiring into cohesive extension methods so <c>Program.cs</c> reads as a
/// high-level outline. These are pure moves of the original inline registrations — no behaviour change.
/// </summary>
public static class ApiConfigurationExtensions
{
    /// <summary>
    /// Binds and validates the strongly-typed options used across the API (JWT, refresh/reset tokens,
    /// account lockout, external providers, AI provider fallback) and configures antiforgery.
    /// </summary>
    public static IServiceCollection AddApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtAuthOptions>(configuration.GetSection("JwtConfig"));
        services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));
        services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));
        services.AddOptions<McpOAuthOptions>()
            .Bind(configuration.GetSection(McpOAuthOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<McpOAuthOptions>, McpOAuthOptionsValidator>();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-Token";
            options.Cookie.Name = "fm_antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        services.AddOptions<AccountLockoutOptions>()
            .Bind(configuration.GetSection(AccountLockoutOptions.SectionName))
            .Validate(o => o.MaxFailedAttempts > 0, "AccountLockout:MaxFailedAttempts must be greater than 0.")
            .Validate(o => o.LockoutDuration > TimeSpan.Zero, "AccountLockout:LockoutDuration must be greater than 0.")
            .ValidateOnStart();
        services.AddOptions<StockApiOptions>()
            .Bind(configuration.GetSection("StockApi"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "StockApi:BaseUrl must be configured.")
            .ValidateOnStart();
        services.Configure<OpenFigiOptions>(configuration.GetSection("OpenFigi"));
        services.AddOptions<EodhdOptions>()
            .Bind(configuration.GetSection("Eodhd"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Eodhd:BaseUrl must be configured.")
            .ValidateOnStart();
        services.AddOptions<TwelveDataOptions>()
            .Bind(configuration.GetSection(TwelveDataOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "TwelveData:BaseUrl must be configured.")
            .Validate(o => o.Priority >= 0, "TwelveData:Priority cannot be negative.")
            .Validate(o => o.PerMinuteCreditLimit > 0, "TwelveData:PerMinuteCreditLimit must be greater than 0.")
            .Validate(o => o.DailyCreditLimit > 0, "TwelveData:DailyCreditLimit must be greater than 0.")
            .Validate(o => o.RequestTimeoutSeconds > 0, "TwelveData:RequestTimeoutSeconds must be greater than 0.")
            .ValidateOnStart();
        services.AddOptions<NbpOptions>()
            .Bind(configuration.GetSection(NbpOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Nbp:BaseUrl must be configured.")
            .ValidateOnStart();
        services.AddOptions<EcbOptions>()
            .Bind(configuration.GetSection(EcbOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Ecb:BaseUrl must be configured.")
            .ValidateOnStart();
        services.Configure<LmStudioOptions>(configuration.GetSection("LmStudio"));
        services.AddOptions<OpenRouterOptions>()
            .Bind(configuration.GetSection("OpenRouter"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "OpenRouter:BaseUrl must be configured.")
            .Validate(o => o.RequestTimeoutSeconds > 0, "OpenRouter:RequestTimeoutSeconds must be greater than 0.")
            .ValidateOnStart();
        services.AddOptions<GitHubModelsOptions>()
            .Bind(configuration.GetSection("GitHubModels"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "GitHubModels:BaseUrl must be configured.")
            .Validate(o => o.RequestTimeoutSeconds > 0, "GitHubModels:RequestTimeoutSeconds must be greater than 0.")
            .ValidateOnStart();
        services.AddOptions<OllamaOptions>()
            .Bind(configuration.GetSection("Ollama"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Ollama:BaseUrl must be configured.")
            .ValidateOnStart();
        services.Configure<AiProviderOptions>(configuration.GetSection("AiProvider"));
        services.Configure<MaintenanceOptions>(configuration.GetSection(MaintenanceOptions.SectionName));
        services.Configure<BackfillOptions>(configuration.GetSection(BackfillOptions.SectionName));
        services.Configure<List<AiProviderFallbackStrategyOption>>(configuration.GetSection("AIProviderFallbackStrategies"));

        return services;
    }

    /// <summary>
    /// Registers the hosted background services and their channels/stores (guest cleanup, insights
    /// generation, label setting, currency import) plus database-backed logging and log retention.
    /// </summary>
    public static IServiceCollection AddApiBackgroundServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IGuestLoginService, GuestLoginService>();
        services.AddSingleton<IGuestSessionStore, GuestSessionStore>();
        services.AddScoped<IGuestSessionAccessor, GuestSessionAccessor>();
        services.AddHostedService<GuestSessionCleanupService>();
        services.AddSingleton<IInsightsGenerationChannel, InsightsGenerationChannel>();
        services.AddHostedService<InsightsGenerationBackgroundService>();
        services.AddSingleton<ILabelSetterProgressTracker, LabelSetterProgressTracker>();
        services.AddSingleton<ILabelSetterChannel, LabelSetterChannel>();
        services.AddHostedService<LabelSetterBackgroundService>();
        services.AddHostedService<LabelSetterStartupService>();
        services.AddSingleton<ICurrencyImportJobChannel, CurrencyImportJobChannel>();
        services.AddSingleton<ICurrencyImportJobStore, CurrencyImportJobStore>();
        services.AddHostedService<CurrencyImportBackgroundService>();
        services.AddSingleton<IPriceBackfillJobChannel, PriceBackfillJobChannel>();
        services.AddHostedService<PriceBackfillBackgroundService>();
        services.AddHostedService<StartupBackfillBackgroundService>();

        services.Configure<LogRetentionOptions>(configuration.GetSection(LogRetentionOptions.SectionName));
        services.AddSingleton<ILogEntryQueue, LogEntryQueue>();
        services.AddSingleton<ILoggerProvider>(sp => new DatabaseLoggerProvider(sp.GetRequiredService<ILogEntryQueue>()));
        services.AddHostedService<LogEntryPersistenceBackgroundService>();
        services.AddHostedService<LogRetentionBackgroundService>();

        return services;
    }
}