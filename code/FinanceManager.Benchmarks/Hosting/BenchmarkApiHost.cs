using FinanceManager.Api;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Benchmarks.Configuration;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// Boots the real API — the full middleware pipeline, MVC, auth, application services, repositories and
/// EF Core — against a local SQLite file, with everything that would add non-deterministic cost removed.
/// </summary>
/// <remarks>
/// <para>
/// What is measured: routing, model binding, JWT validation, authorization, the controller, the
/// application/domain services, the repository layer, EF Core query translation and execution against
/// SQLite, and JSON serialization of the full response body.
/// </para>
/// <para>
/// What is <em>not</em> measured: Kestrel, sockets and TLS. <c>WebApplicationFactory</c> serves requests
/// over an in-memory transport, so the OS network stack is out of scope. That is a deliberate trade:
/// the excluded work is a near-constant per-request cost that a service or query refactor will not
/// change, and excluding it removes the largest source of run-to-run variance. Treat the numbers as
/// server-side work per request, not as end-to-end wire latency.
/// </para>
/// </remarks>
public sealed class BenchmarkApiHost : WebApplicationFactory<ApiEntryPoint>
{
    private readonly string _connectionString;

    static BenchmarkApiHost() =>
        // Each host loads appsettings*.json with reloadOnChange, and every watcher costs an inotify
        // instance. The benchmark process builds a seeding host and a measurement host; the watchers
        // buy nothing here and the sandbox limit is easy to reach.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

    public BenchmarkApiHost(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("benchmark");

        // Program.cs validates most of these while the host is still building, which is too early for
        // ConfigureAppConfiguration to matter — UseSetting lands them in the initial configuration.
        builder.UseSetting("JwtConfig:Key", BenchmarkOptions.JwtSigningKey);
        builder.UseSetting("JwtConfig:Issuer", "FinanceManager.api");
        builder.UseSetting("JwtConfig:Audience", "FinanceManager.Frontend");
        builder.UseSetting("JwtConfig:TokenValidityMins", "600");

        // appsettings.json pins AllowedHosts to the production hostname; the in-memory transport serves
        // http://localhost, which would otherwise be rejected with 400 before reaching a controller.
        builder.UseSetting("AllowedHosts", "*");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://localhost:7206");
        builder.UseSetting("ReverseProxy:KnownProxies:0", "127.0.0.1");

        // A rate limiter would start rejecting requests a few thousand invocations into a run and the
        // benchmark would end up measuring 429s.
        builder.UseSetting("RateLimiting:Enabled", "false");

        // Reaches Alpha Vantage on startup.
        builder.UseSetting("Backfill:RunOnStartup", "false");

        // OpenIddict needs signing certificates and contributes nothing to the endpoints under test.
        builder.UseSetting("McpOAuth:Enabled", "false");

        // Takes the simple AddDbContext branch in AddDatabase rather than the pooled relational one,
        // which gives ConfigureServices a single options descriptor to replace with SQLite below.
        builder.UseSetting("UseInMemoryDatabase", "true");

        builder.ConfigureServices(services =>
        {
            RemoveApplicationBackgroundServices(services);
            UseSqliteDatabase(services);
            UseOfflineExternalProviders(services);

            if (BenchmarkOptions.ColdCache)
            {
                services.RemoveAll<HybridCache>();
                services.AddSingleton<HybridCache, PassThroughHybridCache>();
            }

            // Log writes are synchronous work inside the measured request, so the level is turned down to
            // warnings — but the console provider stays, because an endpoint that starts returning 500
            // has to be diagnosable. Raise it with FM_BENCH_LOG_LEVEL=Information to see EF's SQL.
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole();
                logging.SetMinimumLevel(BenchmarkOptions.LogLevel);
            });
        });
    }

    /// <summary>
    /// Creates the SQLite schema. EF Core migrations in this repo are generated for SQL Server, so they
    /// cannot be replayed against SQLite; the model itself carries no provider-specific column types,
    /// which makes <c>EnsureCreated</c> a faithful equivalent.
    /// </summary>
    public async Task CreateSchema(CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// Fails fast if the host is not actually talking to SQLite. Silent fallback to the in-memory
    /// provider would still answer every request and every number would be meaningless.
    /// </summary>
    public void AssertUsingSqlite()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provider = context.Database.ProviderName;

        if (provider != "Microsoft.EntityFrameworkCore.Sqlite")
            throw new InvalidOperationException(
                $"Benchmark host is using '{provider}' instead of SQLite. The AppDbContext replacement in "
                + $"{nameof(BenchmarkApiHost)}.{nameof(UseSqliteDatabase)} no longer matches how AddDatabase registers the context.");
    }

    private void UseSqliteDatabase(IServiceCollection services)
    {
        // AddDbContext registers the options with TryAdd, so the production registration has to go
        // before ours is visible.
        services.RemoveAll<AppDbContext>();
        services.RemoveAll<DbContextOptions>();
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        RemoveDbContextOptionsConfiguration(services);

        services.AddDbContext<AppDbContext>(options => options
            .UseSqlite(_connectionString)
            .UseOpenIddict()
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>());
    }

    /// <summary>
    /// Drops EF Core's internal per-context options-configuration descriptors, which is where
    /// <c>AddDbContext</c> keeps the provider callback on current EF versions. Matched by name so an EF
    /// upgrade that renames or re-shapes the type cannot leave the in-memory provider wired up
    /// alongside ours — <see cref="AssertUsingSqlite"/> is the backstop if it ever does.
    /// </summary>
    private static void RemoveDbContextOptionsConfiguration(IServiceCollection services)
    {
        var stale = services
            .Where(descriptor => descriptor.ServiceType.IsGenericType
                && descriptor.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)
                && descriptor.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext)))
            .ToList();

        foreach (var descriptor in stale)
            services.Remove(descriptor);
    }

    /// <summary>
    /// Removes FinanceManager's own hosted services. They do migrations, seeding, AI insight generation,
    /// label setting, imports, price backfills and log persistence — none of it serves a request, all of
    /// it competes for the same CPU and SQLite file as the measured iterations.
    /// </summary>
    /// <remarks>
    /// <c>DatabaseInitializer</c> in particular would throw on startup: outside Development it treats
    /// the SQL Server migrations as pending against a SQLite database that has no migration history.
    /// Filtering by assembly rather than by type keeps this working without reaching into the API
    /// project's internals.
    /// </remarks>
    private static void RemoveApplicationBackgroundServices(IServiceCollection services)
    {
        var applicationHostedServices = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                && IsApplicationType(descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType()))
            .ToList();

        foreach (var descriptor in applicationHostedServices)
            services.Remove(descriptor);

        static bool IsApplicationType(Type? type) =>
            type?.Assembly.GetName().Name?.StartsWith("FinanceManager", StringComparison.Ordinal) == true;
    }

    private static void UseOfflineExternalProviders(IServiceCollection services)
    {
        services.RemoveAll<ICurrencyExchangeRateProvider>();
        services.AddScoped<ICurrencyExchangeRateProvider, OfflineCurrencyExchangeRateProvider>();

        services.RemoveAll<IStockPriceSource>();
        services.AddScoped<IStockPriceSource, OfflineStockPriceSource>();
    }
}