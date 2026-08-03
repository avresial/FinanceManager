using FinanceManager.Application.Shared.Ai;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared.Ai.Entities;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Administration.Controllers;

[Trait("Category", "Integration")]
public class AdminAiProvidersControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        var configMock = new Mock<IAiConfigurationService>();
        configMock.Setup(x => x.GetProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string providerName, CancellationToken _) => ValueTask.FromResult(new AiProviderConfiguration
            {
                ProviderName = providerName,
                BaseUrl = $"https://{providerName}.test",
                ApiKey = providerName == "OpenRouter" ? "secret" : string.Empty,
                RequestTimeoutSeconds = 30,
                IsEnabled = true
            }));
        configMock.Setup(x => x.GetAllModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AiProviderModel { Id = 3, ProviderName = "OpenRouter", ModelName = "test-model", IsEnabled = true }]);
        configMock.Setup(x => x.GetFallbackEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AiFallbackEntry { ProviderName = "OpenRouter", Model = "test-model", Order = 0 }]);
        services.AddSingleton(configMock.Object);
    }

    [Fact]
    public async Task GetConfiguration_AsAdmin_ReturnsProviderConfiguration()
    {
        Authorize("admin", 1, UserRole.Admin);

        var response = await Client.GetAsync("api/admin/ai-providers", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("OpenRouter", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test-model", content, StringComparison.OrdinalIgnoreCase);
    }
}