using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using FinanceManager.Domain.Shared.ExternalServices.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Services.ExternalServices;

internal sealed class ExternalServiceConfigService(
    IServiceScopeFactory scopeFactory,
    IOptions<StockApiOptions> stockApiDefaults,
    IOptions<OpenFigiOptions> openFigiDefaults) : IExternalServiceConfigService
{
    private List<ExternalServiceConfiguration>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _cache!.FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
            ?? BuildDefault(serviceName);
    }

    public async ValueTask<IReadOnlyList<ExternalServiceConfiguration>> GetAllServicesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _cache!.AsReadOnly();
    }

    public async Task SaveServiceAsync(ExternalServiceConfiguration config, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExternalServiceConfigRepository>();
        await repo.SaveAsync(config, ct);
        await InvalidateCacheAsync(ct);
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_cache is not null)
            return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache is not null)
                return;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IExternalServiceConfigRepository>();
            _cache = await repo.GetAllAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task InvalidateCacheAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try { _cache = null; }
        finally { _lock.Release(); }
    }

    private ExternalServiceConfiguration BuildDefault(string serviceName) => serviceName switch
    {
        "AlphaVantage" => new ExternalServiceConfiguration
        {
            ServiceName = "AlphaVantage",
            BaseUrl = stockApiDefaults.Value.BaseUrl,
            ApiKey = stockApiDefaults.Value.ApiKey,
            IsEnabled = true,
        },
        "OpenFigi" => new ExternalServiceConfiguration
        {
            ServiceName = "OpenFigi",
            BaseUrl = openFigiDefaults.Value.BaseUrl,
            ApiKey = openFigiDefaults.Value.ApiKey,
            IsEnabled = true,
        },
        _ => new ExternalServiceConfiguration { ServiceName = serviceName, IsEnabled = true },
    };
}