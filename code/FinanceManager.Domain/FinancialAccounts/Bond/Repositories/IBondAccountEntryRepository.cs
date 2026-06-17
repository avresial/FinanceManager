using FinanceManager.Domain.Repositories.Account;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Repositories;

public interface IBondAccountEntryRepository<T> : IAccountEntryRepository<T>
{
    new Task<Dictionary<int, T>> GetNextOlder(int accountId, DateTime date);
    new Task<Dictionary<int, T>> GetNextYounger(int accountId, DateTime date);

    /// <summary>
    /// Batch boundary lookup keyed first by account, then by instrument (bond details id): for each supplied
    /// account returns the newest entry strictly older than <paramref name="date"/> per bond, in a single
    /// query. Used to batch-load a user's bond accounts without a query per account and per bond (issue #411).
    /// </summary>
    Task<Dictionary<int, Dictionary<int, T>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date);

    /// <summary>
    /// Batch boundary lookup keyed by account then bond details id: the oldest entry strictly younger than
    /// <paramref name="date"/> per bond for each account, in a single query.
    /// </summary>
    Task<Dictionary<int, Dictionary<int, T>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date);
}