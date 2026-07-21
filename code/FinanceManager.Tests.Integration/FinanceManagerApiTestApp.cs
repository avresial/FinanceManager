using FinanceManager.Api;
using FinanceManager.Api.Logging;
using FinanceManager.Api.Services;
using FinanceManager.Infrastructure.Services;
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

    public FinanceManagerApiTestApp(Action<IServiceCollection>? services = null, string environmentName = "test")
    {
        _services = services;
        _environmentName = environmentName;
        Client = CreateClient();
        Client.BaseAddress = new Uri("http://localhost/");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
            builder.UseSetting("McpOAuth:Enabled", "true");
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