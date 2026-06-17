using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Domain.Labels.Services;

public interface IRecurringTransactionDetectorService
{
    Task<List<RecurringTransactionResult>> GetRecurringTransactions(int userId, CancellationToken cancellationToken = default);
}