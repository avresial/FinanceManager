using FinanceManager.Domain.Alerts.Entities;

namespace FinanceManager.Domain.Alerts.Repositories;

public interface IFinancialAlertRepository
{
    Task<IReadOnlyList<FinancialAlert>> GetAlertsByUserId(int userId, CancellationToken cancellationToken = default);
    Task<FinancialAlert?> GetById(int userId, Guid alertId, CancellationToken cancellationToken = default);
    Task<FinancialAlert> Add(FinancialAlert alert, CancellationToken cancellationToken = default);
    Task Update(FinancialAlert alert, CancellationToken cancellationToken = default);
    Task<bool> Delete(int userId, Guid alertId, CancellationToken cancellationToken = default);
}