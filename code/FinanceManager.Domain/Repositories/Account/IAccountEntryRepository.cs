namespace FinanceManager.Domain.Repositories.Account;

public interface IAccountEntryRepository<T>
{
    IAsyncEnumerable<T> Get(int accountId, DateTime startDate, DateTime endDate);
    Task<List<T>> Get(int accountId, DateTime date, int count, bool olderThenDate = true);

    /// <summary>
    /// Returns the <c>PostingDate</c> of every entry in the account (one element per entry, including
    /// duplicate dates), projected straight from the index without materialising full entities. Lets a
    /// caller size a transaction-history window cheaply before fetching the entries themselves.
    /// </summary>
    Task<List<DateTime>> GetPostingDates(int accountId);

    /// <summary>
    /// Gets entries within a date range, expanding backward to include at least <paramref name="minimumEntryCount"/>
    /// entries if the range contains fewer. Returns the entries and the effective start date used. Uses at most
    /// two queries: one to project dates, one to fetch the range.
    /// </summary>
    Task<(List<T> Entries, DateTime EffectiveStartDate)> GetEntriesWithMinimumCount(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0);
    Task<T?> Get(int accountId, int entryId);
    Task<IReadOnlyList<T>> GetByIds(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default);
    Task<T?> GetYoungest(int accountId);
    Task<T?> GetNextYounger(int accountId, int entryId);
    Task<T?> GetNextYounger(int accountId, DateTime date);
    Task<T?> GetNextOlder(int accountId, int entryId);
    Task<T?> GetNextOlder(int accountId, DateTime date);
    Task<T?> GetOldest(int accountId);
    Task<int> GetCount(int accountId);

    /// <summary>
    /// Batch variant of <see cref="Get(int, DateTime, DateTime)"/>: returns every entry in the
    /// [<paramref name="startDate"/>, <paramref name="endDate"/>] range for all supplied accounts in a
    /// single query, ordered newest-first. Caller groups the result by <c>AccountId</c>. Loading a whole
    /// user's accounts this way avoids the per-account round trips that caused the dashboard N+1 (issue #411).
    /// </summary>
    Task<List<T>> GetRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Returns, per account, the single newest entry strictly older than <paramref name="date"/> in one
    /// query. Instrument-aware repositories (stock/bond) expose a per-instrument variant of their own.
    /// </summary>
    Task<Dictionary<int, T>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date);

    /// <summary>
    /// Returns, per account, the single oldest entry strictly younger than <paramref name="date"/> in one query.
    /// </summary>
    Task<Dictionary<int, T>> GetNextYounger(IReadOnlyCollection<int> accountIds, DateTime date);

    /// <summary>
    /// Counts entries of this type for each of the supplied users in a single grouped query that joins the
    /// entry table to the accounts table on <c>AccountId</c> and groups by <c>UserId</c>. Users with no
    /// entries of this type are omitted from the result, so callers should treat a missing key as a count
    /// of zero. Used for plan record-capacity calculations.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    Task RecalculateValues(int accountId, int entryId);

    /// <summary>
    /// Recomputes the running <c>Value</c> of every entry in the account from its <c>ValueChange</c>
    /// series, anchored at zero from the oldest entry. Instrument-aware repositories (stock/bond)
    /// recalculate each instrument independently. Used to repair accounts whose stored balances drifted
    /// (e.g. legacy imports that never ran a recalculation pass).
    /// </summary>
    Task RecalculateValues(int accountId);
    Task<bool> Add(T entry, bool recalculate = true);
    Task<bool> Add(IEnumerable<T> entries, bool recalculate = true);
    Task<bool> AddLabel(int entryId, int labelId);
    Task<int> AddLabels(IEnumerable<(int entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default);
    Task<bool> Update(T entry);
    Task<bool> Delete(int accountId, int entryId);
    Task<bool> Delete(int accountId);
}