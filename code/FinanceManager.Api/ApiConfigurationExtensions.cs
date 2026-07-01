using FinanceManager.Api.Logging;
using FinanceManager.Api.Services;
using FinanceManager.Api.Services.Guest;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

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
        services.Configure<List<AiProviderFallbackStrategyOption>>(configuration.GetSection("AIProviderFallbackStrategies"));

        return services;
    }

    /// <summary>
    /// Resolves and validates the JWT signing key, then wires CORS, JWT bearer authentication (including
    /// the SignalR access-token and guest-session hooks) and reverse-proxy forwarded-header handling.
    /// Fails fast outside Development when required secrets/allowlists are missing.
    /// </summary>
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // The JWT signing key must never be committed. It is supplied via environment variable / User Secrets.
        // Fail fast outside Development so we never silently fall back to an insecure default; in Development
        // fall back to a clearly-insecure local key so the app still runs without configured secrets.
        var jwtSigningKey = configuration["JwtConfig:Key"];
        if (string.IsNullOrWhiteSpace(jwtSigningKey))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "JwtConfig:Key is not configured. Provide a signing key via the JwtConfig__Key environment variable or User Secrets.");

            jwtSigningKey = "development-only-insecure-jwt-signing-key-do-not-use-in-production";
            configuration["JwtConfig:Key"] = jwtSigningKey;
        }
        else if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 64)
        {
            // HMAC-SHA512 (used by JwtTokenGenerator) needs a 512-bit (64-byte) key; reject anything
            // weaker so a short env var can't silently produce forgeable tokens or crash at sign time.
            throw new InvalidOperationException(
                "JwtConfig:Key must be at least 64 bytes (512 bits) for HMAC-SHA512 signing.");
        }

        var tokenValidityMins = configuration.GetValue<int>("JwtConfig:TokenValidityMins");
        if (tokenValidityMins <= 0)
            throw new InvalidOperationException(
                "JwtConfig:TokenValidityMins must be greater than 0. Set it to a positive number (e.g. 15) in appsettings or User Secrets.");

        var allowedCorsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Outside Development an empty/missing allowlist must fail fast: never silently fall back to
        // AllowAnyOrigin() in production, which would let any site call the API cross-origin.
        if (allowedCorsOrigins is not { Length: > 0 } && !environment.IsDevelopment())
            throw new InvalidOperationException(
                "Cors:AllowedOrigins is not configured. Provide explicit allowed origins via the " +
                "Cors__AllowedOrigins configuration; AllowAnyOrigin is only permitted in Development.");

        services.AddCors(options =>
        {
            options.AddPolicy("ApiCorsPolicy",
                corsPolicyBuilder =>
                {
                    if (allowedCorsOrigins is { Length: > 0 })
                    {
                        corsPolicyBuilder.WithOrigins(allowedCorsOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod();

                        return;
                    }

                    // Reachable only in Development (the guard above throws otherwise): allow any origin
                    // so local tooling and ad-hoc clients work without configuring an allowlist.
                    corsPolicyBuilder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
        }).AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = configuration["JwtConfig:Issuer"],
                ValidAudience = configuration["JwtConfig:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrWhiteSpace(accessToken) &&
                        (path.StartsWithSegments("/hubs/currency-import")
                         || path.StartsWithSegments("/hubs/label-setter-progress")
                         || path.StartsWithSegments("/hubs/admin-logs")))
                        context.Token = accessToken;

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var principal = context.Principal;
                    var isGuestClaim = principal?.FindFirst(GuestClaims.IsGuest)?.Value;
                    if (!string.Equals(isGuestClaim, "true", StringComparison.OrdinalIgnoreCase))
                        return Task.CompletedTask;

                    var idClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!int.TryParse(idClaim, out var guestUserId))
                    {
                        context.Fail("Guest token is missing a user id.");
                        return Task.CompletedTask;
                    }

                    var store = context.HttpContext.RequestServices.GetRequiredService<IGuestSessionStore>();
                    if (!store.IsActive(guestUserId))
                        context.Fail("Guest session has expired.");

                    return Task.CompletedTask;
                }
            };
        });

        // The app runs behind a TLS-terminating reverse proxy (Cloudflare) in production, so the request
        // arriving at Kestrel is plain HTTP from the proxy. Honour X-Forwarded-Proto/For so Request.IsHttps,
        // Request.Scheme and the remote IP reflect the original client request — this is what makes the
        // Secure refresh-token cookie, HTTPS redirect and per-client rate limiting behave correctly.
        // Only trust headers forwarded by the declared proxy addresses/networks; trusting any source would
        // allow a client that reaches Kestrel directly to spoof scheme or IP.
        var reverseProxyIps = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        var reverseProxyNetworks = configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [];

        if (!environment.IsDevelopment() && reverseProxyIps.Length == 0 && reverseProxyNetworks.Length == 0)
            throw new InvalidOperationException(
                "ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks must be configured outside Development. " +
                "Set these to the IP addresses or CIDR ranges of your reverse proxy (e.g. Cloudflare's published ranges).");

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var proxy in reverseProxyIps)
                if (System.Net.IPAddress.TryParse(proxy, out var ip))
                    options.KnownProxies.Add(ip);

            foreach (var network in reverseProxyNetworks)
                if (System.Net.IPNetwork.TryParse(network, out var net))
                    options.KnownIPNetworks.Add(net);
        });

        return services;
    }

    /// <summary>
    /// Registers the hosted background services and their channels/stores (guest cleanup, insights
    /// generation, label setting, currency import) plus database-backed logging and log retention.
    /// </summary>
    public static IServiceCollection AddApiBackgroundServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<JwtTokenGenerator>();
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

        services.Configure<LogRetentionOptions>(configuration.GetSection(LogRetentionOptions.SectionName));
        services.AddSingleton<ILogEntryQueue, LogEntryQueue>();
        services.AddSingleton<ILoggerProvider>(sp => new DatabaseLoggerProvider(sp.GetRequiredService<ILogEntryQueue>()));
        services.AddHostedService<LogEntryPersistenceBackgroundService>();
        services.AddHostedService<LogRetentionBackgroundService>();

        return services;
    }
}