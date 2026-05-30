using FinanceManager.Api.Logging;
using FinanceManager.Api.Services;
using FinanceManager.Api.Services.Guest;
using FinanceManager.Application;
using FinanceManager.Application.Options;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using ServiceDefaults;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

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
builder.Services.Configure<StockApiOptions>(builder.Configuration.GetSection("StockApi"));
builder.Services.Configure<OpenFigiOptions>(builder.Configuration.GetSection("OpenFigi"));
builder.Services.Configure<LmStudioOptions>(builder.Configuration.GetSection("LmStudio"));
builder.Services.Configure<OpenRouterOptions>(builder.Configuration.GetSection("OpenRouter"));
builder.Services.Configure<GitHubModelsOptions>(builder.Configuration.GetSection("GitHubModels"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<AiProviderOptions>(builder.Configuration.GetSection("AiProvider"));
builder.Services.Configure<List<AiProviderFallbackStrategyOption>>(builder.Configuration.GetSection("AIProviderFallbackStrategies"));

var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();


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
    options.RequireHttpsMetadata = false;
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

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddAuthorization();
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
app.UseAuthorization();

app.MapControllers();
app.MapHub<FinanceManager.Api.Hubs.CurrencyImportHub>("/hubs/currency-import");
app.MapHub<FinanceManager.Api.Hubs.LabelSetterProgressHub>("/hubs/label-setter-progress");
app.MapHub<FinanceManager.Api.Hubs.AdminLogsHub>("/hubs/admin-logs");
app.MapFallbackToFile("index.html");
app.Run();