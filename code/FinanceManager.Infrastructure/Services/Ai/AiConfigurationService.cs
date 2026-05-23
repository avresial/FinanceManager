using FinanceManager.Application.Options;
using FinanceManager.Application.Services.Ai;
using FinanceManager.Domain.Entities.Ai;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class AiConfigurationService(
    IServiceScopeFactory scopeFactory,
    IOptions<OpenRouterOptions> openRouterDefaults,
    IOptions<OllamaOptions> ollamaDefaults,
    IOptions<LmStudioOptions> lmStudioDefaults,
    IOptions<GitHubModelsOptions> gitHubModelsDefaults,
    IOptions<List<AiProviderFallbackStrategyOption>> fallbackStrategyDefaults) : IAiConfigurationService
{
    private static readonly string[] KnownProviders = ["OpenRouter", "LmStudio", "Ollama", "GitHub"];

    private List<AiProviderConfiguration>? _cachedProviders;
    private List<AiFallbackEntry>? _cachedFallback;
    private List<AiProviderModel>? _cachedModels;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async ValueTask<AiProviderConfiguration> GetProviderAsync(string providerName, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _cachedProviders!.FirstOrDefault(p =>
            p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))
            ?? BuildDefault(providerName);
    }

    public async ValueTask<IReadOnlyList<AiFallbackEntry>> GetFallbackEntriesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        if (_cachedFallback!.Count > 0)
            return _cachedFallback;

        // No DB entries yet — convert appsettings defaults so AI works out of the box
        var defaultStrategy = (fallbackStrategyDefaults.Value ?? [])
            .FirstOrDefault(s => s.Name.Equals("default", StringComparison.OrdinalIgnoreCase))
            ?? (fallbackStrategyDefaults.Value ?? []).FirstOrDefault();

        if (defaultStrategy is null)
            return [];

        return defaultStrategy.Providers
            .Where(p => !string.IsNullOrWhiteSpace(p.Provider))
            .Select((p, i) => new AiFallbackEntry
            {
                ProviderName = p.Provider.Trim(),
                Model = p.Model.Trim(),
                Order = i,
            })
            .ToList();
    }

    public async ValueTask<IReadOnlyList<AiProviderConfiguration>> GetAllProvidersAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        // Return only explicitly configured (DB-stored) providers
        return _cachedProviders!.AsReadOnly();
    }

    public async ValueTask<IReadOnlyList<AiProviderModel>> GetAllModelsAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _cachedModels!.AsReadOnly();
    }

    public async Task SaveProviderAsync(AiProviderConfiguration config, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAiProviderConfigRepository>();
        await repo.SaveProviderAsync(config, ct);
        await InvalidateCacheAsync(ct);
    }

    public async Task DeleteProviderAsync(string providerName, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAiProviderConfigRepository>();
        await repo.DeleteProviderAsync(providerName, ct);
        await InvalidateCacheAsync(ct);
    }

    public async Task SaveFallbackEntriesAsync(IReadOnlyList<AiFallbackEntry> entries, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAiProviderConfigRepository>();
        await repo.SaveFallbackEntriesAsync([.. entries], ct);
        await InvalidateCacheAsync(ct);
    }

    public async Task AddModelAsync(AiProviderModel model, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAiProviderConfigRepository>();
        await repo.AddModelAsync(model, ct);
        await InvalidateCacheAsync(ct);
    }

    public async Task UpdateModelAsync(AiProviderModel model, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAiProviderConfigRepository>();
        await repo.UpdateModelAsync(model, ct);
        await InvalidateCacheAsync(ct);
    }

    public async Task DeleteModelAsync(int modelId, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAiProviderConfigRepository>();
        await repo.DeleteModelAsync(modelId, ct);
        await InvalidateCacheAsync(ct);
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_cachedProviders is not null)
            return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedProviders is not null)
                return;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAiProviderConfigRepository>();
            _cachedProviders = await repo.GetProvidersAsync(ct);
            _cachedFallback = await repo.GetFallbackEntriesAsync(ct);
            _cachedModels = await repo.GetAllModelsAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task InvalidateCacheAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _cachedProviders = null;
            _cachedFallback = null;
            _cachedModels = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private AiProviderConfiguration BuildDefault(string providerName) => providerName switch
    {
        "OpenRouter" => new AiProviderConfiguration
        {
            ProviderName = "OpenRouter",
            BaseUrl = openRouterDefaults.Value.BaseUrl,
            ApiKey = openRouterDefaults.Value.ApiKey,
            RequestTimeoutSeconds = openRouterDefaults.Value.RequestTimeoutSeconds,
        },
        "LmStudio" => new AiProviderConfiguration
        {
            ProviderName = "LmStudio",
            BaseUrl = lmStudioDefaults.Value.BaseUrl,
            ApiKey = lmStudioDefaults.Value.ApiKey,
            RequestTimeoutSeconds = lmStudioDefaults.Value.RequestTimeoutSeconds,
        },
        "Ollama" => new AiProviderConfiguration
        {
            ProviderName = "Ollama",
            BaseUrl = ollamaDefaults.Value.BaseUrl,
            RequestTimeoutSeconds = 180,
        },
        "GitHub" => new AiProviderConfiguration
        {
            ProviderName = "GitHub",
            BaseUrl = gitHubModelsDefaults.Value.BaseUrl,
            ApiKey = gitHubModelsDefaults.Value.ApiKey,
            RequestTimeoutSeconds = gitHubModelsDefaults.Value.RequestTimeoutSeconds,
        },
        _ => new AiProviderConfiguration { ProviderName = providerName },
    };
}
