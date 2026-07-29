using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.Labels.Services;

public interface IRecurringTransactionDetectorService
{
    Task<List<RecurringTransactionResult>> GetRecurringTransactions(int userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateSubscription(
        int userId,
        Guid patternId,
        UpdateRecurringSubscription command,
        CancellationToken cancellationToken = default);
}