using FinanceManager.Api;
using FinanceManager.Api.Features.Administration.Hubs;
using FinanceManager.Api.Features.Administration.Logging;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Hubs;
using FinanceManager.Api.Features.Labels.Hubs;
using FinanceManager.Api.Features.Mcp;
using FinanceManager.Api.Shared.Middleware;
using FinanceManager.Application;
using FinanceManager.Application.Shared.Diagnostics;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Surface FinanceManager's own business metrics and traces (see FinanceManagerTelemetry)
// in the Aspire dashboard alongside the defaults wired up by AddServiceDefaults().
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(FinanceManagerTelemetry.MeterName))
    .WithTracing(tracing => tracing.AddSource(FinanceManagerTelemetry.ActivitySourceName));

builder.Services
    .AddProblemDetails()
    .AddExceptionHandler<GlobalExceptionHandler>()
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

builder.Services.AddHybridCache();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = RequestBodySizeLimits.GlobalRequestBodyBytes;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = RequestBodySizeLimits.GlobalRequestBodyBytes;
});

builder.Services.AddApiOptions(builder.Configuration);
builder.Services.AddApiSecurity(builder.Configuration, builder.Environment);
builder.Services.AddMcpFeature(builder.Configuration);

builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddAuthorization();
builder.Services.AddApiHealthChecks();
builder.Services.AddApiBackgroundServices(builder.Configuration);

// Filters must be applied to builder.Logging (not the service collection): keep EF Core, SignalR and
// connection noise out of the database-backed log sink to avoid recursive/self-amplifying logging.
builder.Logging.AddFilter<DatabaseLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter<DatabaseLoggerProvider>("Microsoft.AspNetCore.SignalR", LogLevel.None);
builder.Logging.AddFilter<DatabaseLoggerProvider>("Microsoft.AspNetCore.Http.Connections", LogLevel.None);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.Logger.LogDebug(
        "Password reset endpoints are disabled outside Development until transactional email delivery is implemented.");

// Must run before any middleware that inspects the scheme or client IP (HTTPS redirect, CORS,
// rate limiter): it rewrites Request.Scheme/IsHttps and the remote IP from the proxy's
// X-Forwarded-* headers so the rest of the pipeline sees the original client request.
app.UseForwardedHeaders();

// Registered first so it wraps the whole pipeline: any unhandled exception is turned into a
// consistent ProblemDetails response by GlobalExceptionHandler instead of leaking framework defaults.
app.UseExceptionHandler();

app.UseMiddleware<RequestBodySizeLimitMiddleware>();

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
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.File.Name is "service-worker.js" or "service-worker-assets.js" or "manifest.webmanifest")
        {
            context.Context.Response.Headers.CacheControl = "no-cache";
        }
    }
});

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
app.MapMcpFeature(app.Configuration);
app.MapHub<CurrencyImportHub>("/hubs/currency-import");
app.MapHub<LabelSetterProgressHub>("/hubs/label-setter-progress");
app.MapHub<AdminLogsHub>("/hubs/admin-logs");
app.MapFallbackToFile("index.html");

if (McpClientRevocationCommand.IsRequested(args))
{
    Environment.ExitCode = await McpClientRevocationCommand.RunAsync(args, app.Services);
    return;
}

app.Run();