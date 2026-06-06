using FinanceManager.Api;
using FinanceManager.Api.Logging;
using FinanceManager.Api.Middleware;
using FinanceManager.Api.Services;
using FinanceManager.Api.Services.Guest;
using FinanceManager.Application;
using FinanceManager.Application.Diagnostics;
using FinanceManager.Application.Options;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using ServiceDefaults;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Surface FinanceManager's own business metrics and traces (see FinanceManagerTelemetry)
// in the Aspire dashboard alongside the defaults wired up by AddServiceDefaults().
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(FinanceManagerTelemetry.MeterName))
    .WithTracing(tracing => tracing.AddSource(FinanceManagerTelemetry.ActivitySourceName));

// The JWT signing key must never be committed. It is supplied via environment variable / User Secrets.
// Fail fast outside Development so we never silently fall back to an insecure default; in Development
// fall back to a clearly-insecure local key so the app still runs without configured secrets.
var jwtSigningKey = builder.Configuration["JwtConfig:Key"];
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "JwtConfig:Key is not configured. Provide a signing key via the JwtConfig__Key environment variable or User Secrets.");

    jwtSigningKey = "development-only-insecure-jwt-signing-key-do-not-use-in-production";
    builder.Configuration["JwtConfig:Key"] = jwtSigningKey;
}
else if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    // HMAC-SHA256 needs a 256-bit (32-byte) key; reject anything weaker so a short
    // env var can't silently produce forgeable tokens for both issuing and validation.
    throw new InvalidOperationException(
        "JwtConfig:Key must be at least 32 bytes (256 bits) for HMAC-SHA256 signing.");
}


builder.Services
    .AddProblemDetails()
    .AddExceptionHandler<FinanceManager.Api.Middleware.GlobalExceptionHandler>()
    .AddSingleton(typeof(IOptionsSnapshot<>), typeof(OptionsManager<>))
    .AddSingleton(typeof(IOptionsFactory<>), typeof(OptionsFactory<>))
    .AddOpenApi("v1", options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    })
    .AddDatabase(builder.Configuration)
    .AddApplicationApi()
    .AddInfrastructureApi()
    .AddControllers();


builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection("JwtConfig"));
builder.Services.Configure<RefreshTokenOptions>(builder.Configuration.GetSection(RefreshTokenOptions.SectionName));
builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection(PasswordResetOptions.SectionName));
builder.Services.AddOptions<AccountLockoutOptions>()
    .Bind(builder.Configuration.GetSection(AccountLockoutOptions.SectionName))
    .Validate(o => o.MaxFailedAttempts > 0, "AccountLockout:MaxFailedAttempts must be greater than 0.")
    .Validate(o => o.LockoutDuration > TimeSpan.Zero, "AccountLockout:LockoutDuration must be greater than 0.")
    .ValidateOnStart();
builder.Services.AddOptions<StockApiOptions>()
    .Bind(builder.Configuration.GetSection("StockApi"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "StockApi:BaseUrl must be configured.")
    .ValidateOnStart();
builder.Services.Configure<OpenFigiOptions>(builder.Configuration.GetSection("OpenFigi"));
builder.Services.Configure<LmStudioOptions>(builder.Configuration.GetSection("LmStudio"));
builder.Services.AddOptions<OpenRouterOptions>()
    .Bind(builder.Configuration.GetSection("OpenRouter"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "OpenRouter:BaseUrl must be configured.")
    .Validate(o => o.RequestTimeoutSeconds > 0, "OpenRouter:RequestTimeoutSeconds must be greater than 0.")
    .ValidateOnStart();
builder.Services.AddOptions<GitHubModelsOptions>()
    .Bind(builder.Configuration.GetSection("GitHubModels"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "GitHubModels:BaseUrl must be configured.")
    .Validate(o => o.RequestTimeoutSeconds > 0, "GitHubModels:RequestTimeoutSeconds must be greater than 0.")
    .ValidateOnStart();
builder.Services.AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection("Ollama"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Ollama:BaseUrl must be configured.")
    .ValidateOnStart();
builder.Services.Configure<AiProviderOptions>(builder.Configuration.GetSection("AiProvider"));
builder.Services.Configure<List<AiProviderFallbackStrategyOption>>(builder.Configuration.GetSection("AIProviderFallbackStrategies"));

var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

// Outside Development an empty/missing allowlist must fail fast: never silently fall back to
// AllowAnyOrigin() in production, which would let any site call the API cross-origin.
if (allowedCorsOrigins is not { Length: > 0 } && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "Cors:AllowedOrigins is not configured. Provide explicit allowed origins via the " +
        "Cors__AllowedOrigins configuration; AllowAnyOrigin is only permitted in Development.");


builder.Services.AddCors(options =>
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
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["JwtConfig:Issuer"],
        ValidAudience = builder.Configuration["JwtConfig:Audience"],
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
var reverseProxyIps = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
var reverseProxyNetworks = builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [];

if (!builder.Environment.IsDevelopment() && reverseProxyIps.Length == 0 && reverseProxyNetworks.Length == 0)
    throw new InvalidOperationException(
        "ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks must be configured outside Development. " +
        "Set these to the IP addresses or CIDR ranges of your reverse proxy (e.g. Cloudflare's published ranges).");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
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

builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddAuthorization();
builder.Services.AddApiHealthChecks();
builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddSingleton<IGuestSessionStore, GuestSessionStore>();
builder.Services.AddScoped<IGuestSessionAccessor, GuestSessionAccessor>();
builder.Services.AddHostedService<GuestSessionCleanupService>();
builder.Services.AddSingleton<IInsightsGenerationChannel, InsightsGenerationChannel>();
builder.Services.AddHostedService<InsightsGenerationBackgroundService>();
builder.Services.AddSingleton<ILabelSetterProgressTracker, LabelSetterProgressTracker>();
builder.Services.AddSingleton<ILabelSetterChannel, LabelSetterChannel>();
builder.Services.AddHostedService<LabelSetterBackgroundService>();
builder.Services.AddHostedService<LabelSetterStartupService>();
builder.Services.AddSingleton<ICurrencyImportJobChannel, CurrencyImportJobChannel>();
builder.Services.AddSingleton<ICurrencyImportJobStore, CurrencyImportJobStore>();
builder.Services.AddHostedService<CurrencyImportBackgroundService>();

builder.Services.Configure<LogRetentionOptions>(builder.Configuration.GetSection(LogRetentionOptions.SectionName));
builder.Services.AddSingleton<ILogEntryQueue, LogEntryQueue>();
builder.Services.AddSingleton<ILoggerProvider>(sp => new DatabaseLoggerProvider(sp.GetRequiredService<ILogEntryQueue>()));
builder.Logging.AddFilter<DatabaseLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter<DatabaseLoggerProvider>("Microsoft.AspNetCore.SignalR", LogLevel.None);
builder.Logging.AddFilter<DatabaseLoggerProvider>("Microsoft.AspNetCore.Http.Connections", LogLevel.None);
builder.Services.AddHostedService<LogEntryPersistenceBackgroundService>();
builder.Services.AddHostedService<LogRetentionBackgroundService>();

var app = builder.Build();

// Must run before any middleware that inspects the scheme or client IP (HTTPS redirect, CORS,
// rate limiter): it rewrites Request.Scheme/IsHttps and the remote IP from the proxy's
// X-Forwarded-* headers so the rest of the pipeline sees the original client request.
app.UseForwardedHeaders();

// Registered first so it wraps the whole pipeline: any unhandled exception is turned into a
// consistent ProblemDetails response by GlobalExceptionHandler instead of leaking framework defaults.
app.UseExceptionHandler();

// HSTS only outside Development so local HTTP-only loops aren't pinned to HTTPS in the browser.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Emit X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy and a
// Blazor-WASM-compatible CSP on every response. Placed before UseHttpsRedirection so the headers ride the 307 as well.
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.AddHttpAuthentication("Bearer", bearer =>
        {
            bearer.Token = "your-bearer-token";
        });
    });
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseCors("ApiCorsPolicy");
app.UseAuthentication();

// Placed after authentication so the limiter can partition by authenticated user id when available
// (falling back to the remote IP for anonymous traffic), but before authorization so requests rejected
// with 401/403 are still counted — otherwise protected endpoints could be hammered with invalid tokens
// and bypass even the global limit.
app.UseRateLimiter();

app.UseAuthorization();

// Liveness (/alive), readiness (/health) and the secured detailed view (/health/detail).
// Mapped after auth so /health/detail can require authorization; restricted to the Admin role
// to match the app's other operational surfaces (admin logs, AI providers).
app.MapDefaultEndpoints("Admin");

app.MapControllers();
app.MapHub<FinanceManager.Api.Hubs.CurrencyImportHub>("/hubs/currency-import");
app.MapHub<FinanceManager.Api.Hubs.LabelSetterProgressHub>("/hubs/label-setter-progress");
app.MapHub<FinanceManager.Api.Hubs.AdminLogsHub>("/hubs/admin-logs");
app.MapFallbackToFile("index.html");
app.Run();