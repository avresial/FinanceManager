using FinanceManager.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinanceManager.Tests.Integration.Shared;

[Collection("api")]
public sealed class CorsPolicyTests
{
    // Configured in appsettings.test.json, which the "test" environment host loads.
    private const string _configuredOrigin = "https://localhost:7206";

    [Fact]
    public async Task Preflight_From_Configured_Origin_Echoes_Allow_Origin_Header()
    {
        using var app = new FinanceManagerApiTestApp();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/alive");
        request.Headers.Add("Origin", _configuredOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await app.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(_configuredOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Preflight_From_Unconfigured_Origin_Is_Not_Allowed()
    {
        using var app = new FinanceManagerApiTestApp();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/alive");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await app.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Missing_Allowed_Origins_Outside_Development_Fails_Fast()
    {
        using var factory = new NoCorsOriginsApp();

        // The startup guard throws while the host is being built, which WebApplicationFactory
        // surfaces when the server is first materialised.
        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Cors:AllowedOrigins", FlattenMessages(exception));
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" | ", messages);
    }

    // A non-Development host whose configured CORS allowlist is empty, used to prove the startup
    // guard fails fast instead of silently falling back to AllowAnyOrigin(). Running under the
    // "Production" environment (rather than "test") means appsettings.test.json's allowlist is not
    // loaded, so only the single origin from appsettings.json is present to be blanked out.
    private sealed class NoCorsOriginsApp : WebApplicationFactory<ApiEntryPoint>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UseInMemoryDatabase"] = "true",
                    ["Cors:AllowedOrigins:0"] = "",
                });
            });

            return base.CreateHost(builder);
        }
    }
}