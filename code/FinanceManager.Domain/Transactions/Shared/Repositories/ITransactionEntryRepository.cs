namespace FinanceManager.Domain.Transactions.Shared.Repositories;

public interface ITransactionEntryRepository<T>
{
    /// <summary>The owner's entries with exactly these ids, with their labels. Ids that are missing or belong to someone else are skipped; a zero owner id returns nothing.</summary>
    Task<IReadOnlyList<T>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entries of this type for each of the supplied users in a single grouped query on the entry's
    /// own <c>UserId</c>. Users with no entries of
    /// this type are omitted from the result, so callers should treat a missing key as a count of zero.
    /// Used for plan record-capacity calculations.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    Task<bool> Add(T entry, CancellationToken cancellationToken = default);
    Task<bool> Add(IEnumerable<T> entries, CancellationToken cancellationToken = default);

    /// <summary>Attaches labels to the owner's entries and returns how many were newly attached. Assignments for missing or foreign entries are ignored.</summary>
    Task<int> AddLabels(int userId, IEnumerable<(long entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default);

    // ----- Owner-scoped access: the entry's own UserId is the only ownership check -----

    /// <summary>One entry of the owner with its labels; null when it is missing or belongs to someone else.</summary>
    Task<T?> GetByOwner(int userId, long entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The owner's entries of one partition (currency or bond definition) between two timestamps (both inclusive),
    /// oldest first, without labels. Streams from the database.
    /// </summary>
    IAsyncEnumerable<T> GetByOwner(int userId, int partitionId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the editable fields and labels of the owner's entry. Ownership comes from <paramref name="userId"/>.
    /// Returns false when the entry is missing or belongs to someone else.
    /// </summary>
    Task<bool> UpdateByOwner(int userId, T entry, CancellationToken cancellationToken = default);

    /// <summary>Deletes the owner's entry. Returns false when the entry is missing or belongs to someone else.</summary>
    Task<bool> DeleteByOwner(int userId, long entryId, CancellationToken cancellationToken = default);
}