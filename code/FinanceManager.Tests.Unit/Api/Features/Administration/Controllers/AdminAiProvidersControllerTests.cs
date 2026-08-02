using FinanceManager.Api.Features.Administration.Controllers;
using FinanceManager.Application.Shared.Ai;
using FinanceManager.Domain.Administration;
using FinanceManager.Domain.Shared.Ai.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Features.Administration.Controllers;

[Trait("Category", "Unit")]
public class AdminAiProvidersControllerTests
{
    private readonly Mock<IAiConfigurationService> _configService = new();
    private readonly AdminAiProvidersController _controller;

    public AdminAiProvidersControllerTests()
    {
        _configService.Setup(x => x.GetFallbackEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _configService.Setup(x => x.GetAllModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _controller = new AdminAiProvidersController(_configService.Object);
    }

    private void SetupProvider(string providerName, string apiKey) =>
        _configService.Setup(x => x.GetProviderAsync(providerName, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AiProviderConfiguration
            {
                ProviderName = providerName,
                BaseUrl = $"https://{providerName}.test",
                ApiKey = apiKey,
                RequestTimeoutSeconds = 120,
                IsEnabled = true,
            }));

    private void SetupAllProviders(string apiKey = "")
    {
        foreach (var name in (string[])["OpenRouter", "GitHub", "Ollama", "LmStudio"])
            SetupProvider(name, apiKey);
    }

    [Fact]
    public async Task GetConfiguration_ListsEveryKnownProvider_EvenWhenNoneIsStored()
    {
        SetupAllProviders();

        var result = await _controller.GetConfiguration(TestContext.Current.CancellationToken);

        var response = Assert.IsType<AiConfigurationResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(
            ["OpenRouter", "GitHub", "Ollama", "LmStudio"],
            response.Providers.Select(p => p.ProviderName));
        Assert.All(response.Providers, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.False(string.IsNullOrWhiteSpace(p.DocsUrl));
        });
    }

    [Fact]
    public async Task GetConfiguration_ReportsResolvedApiKeyStatusPerProvider()
    {
        SetupAllProviders();
        SetupProvider("OpenRouter", "configured-key");

        var result = await _controller.GetConfiguration(TestContext.Current.CancellationToken);

        var response = Assert.IsType<AiConfigurationResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(response.Providers.Single(p => p.ProviderName == "OpenRouter").HasApiKey);
        Assert.False(response.Providers.Single(p => p.ProviderName == "Ollama").HasApiKey);
    }

    [Fact]
    public async Task UpdateProvider_SavesUnderTheCatalogueSpelling()
    {
        SetupAllProviders();
        AiProviderConfiguration? saved = null;
        _configService.Setup(x => x.SaveProviderAsync(It.IsAny<AiProviderConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<AiProviderConfiguration, CancellationToken>((config, _) => saved = config)
            .Returns(Task.CompletedTask);

        var result = await _controller.UpdateProvider(
            "openrouter",
            new UpdateProviderRequest("https://openrouter.test", "new-key", 60, true),
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("OpenRouter", saved?.ProviderName);
        Assert.Equal("new-key", saved?.ApiKey);
    }

    [Fact]
    public async Task UpdateProvider_KeepsTheStoredKey_WhenNoNewKeyIsSupplied()
    {
        SetupAllProviders();
        SetupProvider("OpenRouter", "existing-key");
        AiProviderConfiguration? saved = null;
        _configService.Setup(x => x.SaveProviderAsync(It.IsAny<AiProviderConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<AiProviderConfiguration, CancellationToken>((config, _) => saved = config)
            .Returns(Task.CompletedTask);

        var result = await _controller.UpdateProvider(
            "OpenRouter",
            new UpdateProviderRequest("https://openrouter.test", null, 60, true),
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("existing-key", saved?.ApiKey);
    }

    [Fact]
    public async Task UpdateProvider_UnknownProviderIsBadRequest()
    {
        var result = await _controller.UpdateProvider(
            "NotAProvider",
            new UpdateProviderRequest("https://example.test", "key", 60, true),
            TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        _configService.Verify(x => x.SaveProviderAsync(It.IsAny<AiProviderConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddModel_UnknownProviderIsBadRequest()
    {
        var result = await _controller.AddModel("NotAProvider", new AddModelRequest("some-model"), TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        _configService.Verify(x => x.AddModelAsync(It.IsAny<AiProviderModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}