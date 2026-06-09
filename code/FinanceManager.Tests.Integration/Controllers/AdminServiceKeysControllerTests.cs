using FinanceManager.Application.Services.ExternalServices;
using FinanceManager.Domain.Entities.ExternalServices;
using FinanceManager.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class AdminServiceKeysControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        var configMock = new Mock<IExternalServiceConfigService>();
        configMock.Setup(x => x.GetServiceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string serviceName, CancellationToken _) => new ExternalServiceConfiguration
            {
                ServiceName = serviceName,
                BaseUrl = $"https://example.test/{serviceName}",
                ApiKey = serviceName == "AlphaVantage" ? "secret" : string.Empty,
                IsEnabled = true
            });
        services.AddSingleton(configMock.Object);
    }

    [Fact]
    public async Task GetConfiguration_AsAdmin_ReturnsKnownServices()
    {
        Authorize("admin", 1, UserRole.Admin);

        var response = await Client.GetAsync("api/admin/service-keys", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("AlphaVantage", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OpenFigi", content, StringComparison.OrdinalIgnoreCase);
    }
}