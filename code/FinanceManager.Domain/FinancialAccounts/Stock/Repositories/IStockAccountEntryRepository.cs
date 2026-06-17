using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
namespace FinanceManager.Domain.FinancialAccounts.Stock.Repositories;

public interface IStockAccountEntryRepository<T> : IAccountEntryRepository<T>
{
    new Task<Dictionary<string, T>> GetNextOlder(int accountId, DateTime date);
    new Task<Dictionary<string, T>> GetNextYounger(int accountId, DateTime date);

    /// <summary>
    /// Batch boundary lookup keyed first by account, then by instrument (ISIN): for each supplied account
    /// returns the newest entry strictly older than <paramref name="date"/> per ISIN, in a single query.
    /// Used to batch-load a user's stock accounts without a query per account and per ISIN (issue #411).
    /// </summary>
    Task<Dictionary<int, Dictionary<string, T>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date);

    /// <summary>
    /// Batch boundary lookup keyed by account then ISIN: the oldest entry strictly younger than
    /// <paramref name="date"/> per ISIN for each account, in a single query.
    /// </summary>
    Task<Dictionary<int, Dictionary<string, T>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date);
}