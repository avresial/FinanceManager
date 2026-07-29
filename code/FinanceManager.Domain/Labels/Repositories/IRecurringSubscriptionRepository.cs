using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Domain.Labels.Repositories;

public interface IRecurringSubscriptionRepository
{
    Task<List<RecurringSubscription>> GetAll(int userId, CancellationToken cancellationToken = default);
    Task Save(IEnumerable<RecurringSubscription> added, CancellationToken cancellationToken = default);
    Task<bool> Update(
        int userId,
        Guid id,
        UpdateRecurringSubscription command,
        CancellationToken cancellationToken = default);
}