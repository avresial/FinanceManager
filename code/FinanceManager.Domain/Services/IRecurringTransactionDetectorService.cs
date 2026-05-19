using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IRecurringTransactionDetectorService
{
    Task<List<NameValueResult>> GetRecurringTransactions(int userId, CancellationToken cancellationToken = default);
}
