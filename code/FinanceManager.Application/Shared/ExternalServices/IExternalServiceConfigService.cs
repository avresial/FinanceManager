using FinanceManager.Domain.Entities.ExternalServices;

namespace FinanceManager.Application.Shared.ExternalServices;

public interface IExternalServiceConfigService
{
    ValueTask<ExternalServiceConfiguration> GetServiceAsync(string serviceName, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ExternalServiceConfiguration>> GetAllServicesAsync(CancellationToken ct = default);
    Task SaveServiceAsync(ExternalServiceConfiguration config, CancellationToken ct = default);
}