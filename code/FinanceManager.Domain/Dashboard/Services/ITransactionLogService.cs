using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Domain.Dashboard.Services;

public interface ITransactionLogService
{
    /// <summary>
    /// Returns the user's <paramref name="count"/> most recent transactions across all of their
    /// accounts regardless of account type, ordered newest first.
    /// </summary>
    Task<List<TransactionLogEntryDto>> GetLastTransactions(int userId, int count, CancellationToken cancellationToken = default);
}