using FinanceManager.Domain.Entities.Ai;

namespace FinanceManager.Application.Services.Ai;

public interface IAiConfigurationService
{
    ValueTask<AiProviderConfiguration> GetProviderAsync(string providerName, CancellationToken ct = default);
    ValueTask<IReadOnlyList<AiFallbackEntry>> GetFallbackEntriesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<AiProviderConfiguration>> GetAllProvidersAsync(CancellationToken ct = default);
    Task SaveProviderAsync(AiProviderConfiguration config, CancellationToken ct = default);
    Task SaveFallbackEntriesAsync(IReadOnlyList<AiFallbackEntry> entries, CancellationToken ct = default);
}
