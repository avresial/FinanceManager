using FinanceManager.Domain.Shared.ExternalServices.Entities;

namespace FinanceManager.Domain.Shared.ExternalServices.Repositories;

public interface IExternalServiceConfigRepository
{
    Task<List<ExternalServiceConfiguration>> GetAllAsync(CancellationToken ct = default);
    Task<ExternalServiceConfiguration?> GetByNameAsync(string serviceName, CancellationToken ct = default);
    Task SaveAsync(ExternalServiceConfiguration config, CancellationToken ct = default);
}