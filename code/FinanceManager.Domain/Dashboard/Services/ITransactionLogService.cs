using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Domain.Dashboard.Services;

public interface ITransactionLogService
{
    /// <summary>
    /// Returns the user's <paramref name="count"/> most recent transactions across all of their
    /// accounts regardless of account type, ordered newest first.
    /// </summary>
    Task<List<TransactionLogEntryDto>> GetLastTransactions(int userId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user has recorded anything at all: a cash entry, a bond entry, a trade or a dividend payment.
    /// Each source is probed with a short-circuiting existence query, so no history is read.
    /// </summary>
    Task<bool> HasAny(int userId, CancellationToken cancellationToken = default);

    Task<TransactionLogItemsPage> GetItemsPage(int userId, TransactionLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>The currencies of the user's own transactions, sorted, for the currency filter options.</summary>
    Task<List<string>> GetCurrencies(int userId, CancellationToken cancellationToken = default);
}