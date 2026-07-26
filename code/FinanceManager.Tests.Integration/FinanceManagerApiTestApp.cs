using FinanceManager.Api;
using FinanceManager.Api.Features.Administration.Logging;
using FinanceManager.Api.Features.Labels.Services;
using FinanceManager.Infrastructure.Services;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceManager.Tests.Integration;

internal sealed class FinanceManagerApiTestApp : WebApplicationFactory<ApiEntryPoint>
{
    private readonly string _environmentName;
    private readonly Action<IServiceCollection>? _services;
    private readonly IReadOnlyDictionary<string, string?>? _hostSettings;

    static FinanceManagerApiTestApp()
    {
        // Each host built by WebApplicationFactory loads appsettings*.json with reloadOnChange=true,
        // which registers FileSystemWatchers (one inotify instance each). On Linux sandboxes the
        // 128-instance limit is exhausted part-way through a full integration run, and later
        // WebApplicationFactory constructions throw at host build time. The hosting builder honours
        // this env var to skip the watcher entirely.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
    }

    public HttpClient Client { get; }

    public FinanceManagerApiTestApp(
        Action<IServiceCollection>? services = null,
        string environmentName = "test",
        IReadOnlyDictionary<string, string?>? hostSettings = null)
    {
        _services = services;
        _environmentName = environmentName;
        _hostSettings = hostSettings;
        Client = CreateClient();
        Client.BaseAddress = new Uri("http://localhost/");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // WebApplicationFactory instances must never fall through to the production SQL provider when a
        // controller test doesn't replace AppDbContext itself. Each factory receives an isolated EF in-memory
        // root, which is sufficient for host-level integration tests and avoids external database dependencies.
        builder.UseSetting("UseInMemoryDatabase", "true");

        if (_hostSettings is not null)
        {
            foreach (var setting in _hostSettings)
                builder.UseSetting(setting.Key, setting.Value);
        }

        builder.ConfigureServices(s =>
        {
            // Remove hosted services that access DB on startup to avoid
            // race conditions with the singleton in-memory test context.
            var databaseInitializerDescriptor = s.FirstOrDefault(d => d.ImplementationType == typeof(DatabaseInitializer));
            if (databaseInitializerDescriptor != null)
                s.Remove(databaseInitializerDescriptor);

            var labelSetterDescriptor = s.FirstOrDefault(d => d.ImplementationType == typeof(LabelSetterStartupService));
            if (labelSetterDescriptor != null)
                s.Remove(labelSetterDescriptor);

            var logPersistenceDescriptor = s.FirstOrDefault(d => d.ImplementationType == typeof(LogEntryPersistenceBackgroundService));
            if (logPersistenceDescriptor != null)
                s.Remove(logPersistenceDescriptor);

            var logRetentionDescriptor = s.FirstOrDefault(d => d.ImplementationType == typeof(LogRetentionBackgroundService));
            if (logRetentionDescriptor != null)
                s.Remove(logRetentionDescriptor);

            _services?.Invoke(s);
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                // Rate limiting is off by default so the broader integration suite isn't throttled by
                // shared-loopback partitioning; RateLimitingTests re-enables it with tiny limits.
                ["RateLimiting:Enabled"] = "false",
                // appsettings.json restricts AllowedHosts to the production hostname; the test host serves
                // requests over http://localhost, so relax host filtering here or every request 400s.
                ["AllowedHosts"] = "*",
                // The startup backfill reaches out to Alpha Vantage; keep it off in tests so the host
                // starts deterministically without external calls or DB races on the in-memory context.
                ["Backfill:RunOnStartup"] = "false",
            };

            config.AddInMemoryCollection(settings);
        });

        // Environment-specific overlays load by environment name, so a host booted under a non-"test" name
        // (e.g. the develop-login blocked-environment tests) would miss the JWT/CORS/proxy settings that
        // appsettings.test.json supplies and that the API validates while Program.cs runs. UseSetting feeds
        // the values into the host's initial configuration, early enough for those startup reads — the
        // ConfigureAppConfiguration callback above applies too late for them.
        if (!string.Equals(_environmentName, "test", StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSetting("JwtConfig:Issuer", "FinanceManager.api");
            builder.UseSetting("JwtConfig:Audience", "FinanceManager.Frontend");
            builder.UseSetting("JwtConfig:TokenValidityMins", "60");
            builder.UseSetting("ReverseProxy:KnownProxies:0", "127.0.0.1");
            builder.UseSetting("Cors:AllowedOrigins:0", "https://localhost:7206");
            // Non-Test environment hosts exercise unrelated production guards and do not carry real OAuth
            // certificates. Keep the rollout gate closed there; dedicated OAuth tests run in Test with
            // ephemeral keys, while unit tests cover the production certificate requirements.
            builder.UseSetting("McpOAuth:Enabled", "false");
            builder.UseSetting("McpOAuth:Issuer", "https://localhost/");
            builder.UseSetting("McpOAuth:Resource", "https://localhost/mcp");
            builder.UseSetting("McpOAuth:LoginUrl", "https://localhost/login");
            builder.UseSetting("McpOAuth:SigningCertificatePath", "test-signing.pfx");
            builder.UseSetting("McpOAuth:EncryptionCertificatePath", "test-encryption.pfx");
            builder.UseSetting("McpOAuth:Clients:0:ClientId", "finance-manager-mcp-test");
            builder.UseSetting("McpOAuth:Clients:0:DisplayName", "Finance Manager MCP (Test)");
            builder.UseSetting("McpOAuth:Clients:0:RequirePkce", "true");
            builder.UseSetting("McpOAuth:Clients:0:RedirectUris:0", "https://localhost/oauth/callback");
        }

        builder.UseEnvironment(_environmentName);
    }
}