using FinanceManager.Domain.Entities.Ai;

namespace FinanceManager.Domain.Repositories;

public interface IAiProviderConfigRepository
{
    Task<List<AiProviderConfiguration>> GetProvidersAsync(CancellationToken ct = default);
    Task<List<AiFallbackEntry>> GetFallbackEntriesAsync(CancellationToken ct = default);
    Task<List<AiProviderModel>> GetAllModelsAsync(CancellationToken ct = default);
    Task SaveProviderAsync(AiProviderConfiguration config, CancellationToken ct = default);
    Task DeleteProviderAsync(string providerName, CancellationToken ct = default);
    Task SaveFallbackEntriesAsync(List<AiFallbackEntry> entries, CancellationToken ct = default);
    Task AddModelAsync(AiProviderModel model, CancellationToken ct = default);
    Task UpdateModelAsync(AiProviderModel model, CancellationToken ct = default);
    Task DeleteModelAsync(int modelId, CancellationToken ct = default);
}
