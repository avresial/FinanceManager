using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.OAuth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace FinanceManager.Tests.Unit.Infrastructure.OAuth;

public class McpOAuthTests
{
    [Fact]
    public void Validator_AcceptsLoopbackHttpInDevelopment()
    {
        var result = CreateValidator(Environments.Development).Validate(null, CreateOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_RequiresHttpsAndPersistentCertificatesInProduction()
    {
        var result = CreateValidator(Environments.Production).Validate(null, CreateOptions());

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, failure => failure.Contains("HTTPS", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("SigningCertificatePath", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("EncryptionCertificatePath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsNonPositiveTokenAndSessionLifetimes()
    {
        var options = CreateOptions(lifetime: TimeSpan.Zero);

        var result = CreateValidator(Environments.Development).Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Equal(4, result.Failures!.Count(failure => failure.Contains("Lifetime", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Reconciler_ReplacesRedirectUrisPermissionsAndPkceRequirement()
    {
        var services = new ServiceCollection();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options
            .UseSqlite(connection)
            .UseOpenIddict());
        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>());
        services.AddScoped<McpOAuthConfigurationReconciler>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var reconciler = scope.ServiceProvider.GetRequiredService<McpOAuthConfigurationReconciler>();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        await reconciler.ReconcileAsync(CreateOptions("http://localhost/old", "http://localhost/old-mcp", requirePkce: false), TestContext.Current.CancellationToken);
        await reconciler.ReconcileAsync(CreateOptions("http://localhost/new", requirePkce: true), TestContext.Current.CancellationToken);

        var application = await applications.FindByClientIdAsync("finance-manager-mcp-test", TestContext.Current.CancellationToken);
        Assert.NotNull(application);
        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, application, TestContext.Current.CancellationToken);

        Assert.Equal([new Uri("http://localhost/new")], descriptor.RedirectUris);
        Assert.Contains(Permissions.Prefixes.Resource + "http://localhost/mcp", descriptor.Permissions);
        Assert.DoesNotContain(Permissions.Prefixes.Resource + "http://localhost/old-mcp", descriptor.Permissions);
        Assert.Contains(Requirements.Features.ProofKeyForCodeExchange, descriptor.Requirements);

        var scopeEntity = await scopes.FindByNameAsync("mcp", TestContext.Current.CancellationToken);
        Assert.NotNull(scopeEntity);
        var scopeDescriptor = new OpenIddictScopeDescriptor();
        await scopes.PopulateAsync(scopeDescriptor, scopeEntity, TestContext.Current.CancellationToken);
        Assert.Equal(["http://localhost/mcp"], scopeDescriptor.Resources);

        await reconciler.ReconcileAsync(CreateOptions(clientId: "finance-manager-mcp-replacement"), TestContext.Current.CancellationToken);
        Assert.Null(await applications.FindByClientIdAsync("finance-manager-mcp-test", TestContext.Current.CancellationToken));
        Assert.NotNull(await applications.FindByClientIdAsync("finance-manager-mcp-replacement", TestContext.Current.CancellationToken));
    }

    private static McpOAuthOptionsValidator CreateValidator(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
        return new McpOAuthOptionsValidator(environment.Object);
    }

    private static McpOAuthOptions CreateOptions(
        string redirectUri = "http://localhost/callback",
        string resource = "http://localhost/mcp",
        bool requirePkce = true,
        string clientId = "finance-manager-mcp-test",
        TimeSpan? lifetime = null) => new()
        {
            Enabled = true,
            Issuer = "http://localhost/",
            Resource = resource,
            LoginUrl = "http://localhost/login",
            AuthorizationCodeLifetime = lifetime ?? TimeSpan.FromMinutes(2),
            AccessTokenLifetime = lifetime ?? TimeSpan.FromMinutes(15),
            RefreshTokenLifetime = lifetime ?? TimeSpan.FromDays(30),
            AuthorizationSessionLifetime = lifetime ?? TimeSpan.FromMinutes(10),
            Clients =
            [
                new McpOAuthClientOptions
                {
                    ClientId = clientId,
                    DisplayName = "Finance Manager MCP (Test)",
                    RequirePkce = requirePkce,
                    RedirectUris = [redirectUri]
                }
            ]
        };
}