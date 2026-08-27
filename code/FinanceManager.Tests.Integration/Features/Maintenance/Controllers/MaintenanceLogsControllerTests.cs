using FinanceManager.Application.Shared.Maintenance;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Maintenance.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class MaintenanceLogsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _maintenanceKey = "integration-test-maintenance-key";

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.PostConfigure<MaintenanceOptions>(options => options.ApiKey = _maintenanceKey);

        var repositoryMock = new Mock<ILogEntryRepository>();
        repositoryMock
            .Setup(x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyCollection<LogSeverity>?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                [new LogEntry
                {
                    Id = 7,
                    TimestampUtc = DateTime.UtcNow,
                    Level = LogSeverity.Error,
                    Category = "MaintenanceTest",
                    Message = "A provider failure",
                    Exception = "TimeoutException",
                    EventId = 100,
                    EventName = "ProviderFailure"
                }],
                1));
        services.AddSingleton(repositoryMock.Object);
    }

    [Fact]
    public async Task Get_WithValidKey_ReturnsPagedLogEntries()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/maintenance/logs?level=Error&take=10");
        request.Headers.Add("X-Maintenance-Key", _maintenanceKey);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("A provider failure", content, StringComparison.Ordinal);
        Assert.Contains("TimeoutException", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("api/maintenance/logs", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/maintenance/logs");
        request.Headers.Add("X-Maintenance-Key", "wrong-key");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

[Collection("api")]
[Trait("Category", "Integration")]
public class MaintenanceLogsControllerUnconfiguredTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    [Fact]
    public async Task Get_WithoutConfiguredKey_ReturnsNotFound()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/maintenance/logs");
        request.Headers.Add("X-Maintenance-Key", "any-key");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}