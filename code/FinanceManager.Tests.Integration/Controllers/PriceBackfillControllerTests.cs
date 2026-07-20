using FinanceManager.Application.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class PriceBackfillControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _maintenanceKey = "integration-test-maintenance-key";

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.PostConfigure<MaintenanceOptions>(options => options.ApiKey = _maintenanceKey);
    }

    [Fact]
    public async Task Trigger_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("api/PriceBackfill", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Trigger_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/PriceBackfill");
        request.Headers.Add("X-Maintenance-Key", "wrong-key");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Trigger_WithValidKey_ReturnsAccepted()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/PriceBackfill");
        request.Headers.Add("X-Maintenance-Key", _maintenanceKey);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}

/// <summary>
/// Runs against the default test app, whose configuration leaves <c>Maintenance:ApiKey</c> empty —
/// the endpoint must be indistinguishable from an unmapped route in that state.
/// </summary>
[Collection("api")]
[Trait("Category", "Integration")]
public class PriceBackfillControllerUnconfiguredTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    [Fact]
    public async Task Trigger_WithoutConfiguredKey_ReturnsNotFound()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/PriceBackfill");
        request.Headers.Add("X-Maintenance-Key", "any-key");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}