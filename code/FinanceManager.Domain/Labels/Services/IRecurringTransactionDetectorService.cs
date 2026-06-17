using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.Labels.Services;

public interface IRecurringTransactionDetectorService
{
    Task<List<RecurringTransactionResult>> GetRecurringTransactions(int userId, CancellationToken cancellationToken = default);
}