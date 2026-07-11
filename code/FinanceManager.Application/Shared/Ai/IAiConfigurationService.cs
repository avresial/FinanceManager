using FinanceManager.Domain.Shared.Ai.Entities;

namespace FinanceManager.Application.Shared.Ai;

public interface IAiConfigurationService
{
    ValueTask<AiProviderConfiguration> GetProviderAsync(string providerName, CancellationToken ct = default);
    ValueTask<IReadOnlyList<AiFallbackEntry>> GetFallbackEntriesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<AiProviderConfiguration>> GetAllProvidersAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<AiProviderModel>> GetAllModelsAsync(CancellationToken ct = default);
    Task SaveProviderAsync(AiProviderConfiguration config, CancellationToken ct = default);
    Task DeleteProviderAsync(string providerName, CancellationToken ct = default);
    Task SaveFallbackEntriesAsync(IReadOnlyList<AiFallbackEntry> entries, CancellationToken ct = default);
    Task AddModelAsync(AiProviderModel model, CancellationToken ct = default);
    Task UpdateModelAsync(AiProviderModel model, CancellationToken ct = default);
    Task DeleteModelAsync(int modelId, CancellationToken ct = default);
}