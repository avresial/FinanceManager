using FinanceManager.Domain.Entities.ExternalServices;

namespace FinanceManager.Domain.Repositories;

public interface IExternalServiceConfigRepository
{
    Task<List<ExternalServiceConfiguration>> GetAllAsync(CancellationToken ct = default);
    Task<ExternalServiceConfiguration?> GetByNameAsync(string serviceName, CancellationToken ct = default);
    Task SaveAsync(ExternalServiceConfiguration config, CancellationToken ct = default);
}