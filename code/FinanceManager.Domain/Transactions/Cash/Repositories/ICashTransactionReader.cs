using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Cash.Repositories;

public interface ICashTransactionReader
{
    Task<IReadOnlyList<CashLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default);

    /// <summary>
    /// The owner's entries with exactly these ids, linked to an account or not, with their labels, newest first (posting date, then entry id, descending).
    /// Ids that are missing or belong to someone else are skipped; duplicates count once.
    /// </summary>
    Task<IReadOnlyList<CashLogEntry>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken ct = default);

    /// <summary>Distinct short names of the currencies the user has cash entries in.</summary>
    Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default);

    /// <summary>
    /// The pooled balance per currency (sum of <c>ValueChange</c>, linked to an account or not) of entries posted on or before <paramref name="asOf"/>.
    /// Currencies without entries are omitted; net zero and negative balances are kept.
    /// </summary>
    Task<IReadOnlyList<OwnerLedgerPosition>> GetPositions(int userId, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// The pooled balance per currency before <paramref name="start"/> plus every entry in [<paramref name="start"/>, <paramref name="end"/>]
    /// ordered by posting date, then entry id. Returns an empty range when <paramref name="end"/> is before <paramref name="start"/>.
    /// </summary>
    Task<OwnerLedgerRange> GetValueRange(int userId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>True when the user has at least one cash entry, linked to an account or not.</summary>
    Task<bool> HasAnyEntries(int userId, CancellationToken ct = default);

    /// <summary>
    /// The summed <c>ValueChange</c> per label id of the entries posted in [<paramref name="start"/>, <paramref name="end"/>] (both inclusive).
    /// An entry with several labels counts towards each of them; labels without entries are omitted.
    /// </summary>
    Task<IReadOnlyDictionary<int, decimal>> GetLabelTotals(int userId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// The entries posted in [<paramref name="start"/>, <paramref name="end"/>] (<paramref name="end"/> excluded unless <paramref name="endInclusive"/>) with the ids of all their labels.
    /// <paramref name="outflowsOnly"/> keeps negative changes; <paramref name="labelId"/> keeps entries carrying that label. Returns an empty list when <paramref name="end"/> is before <paramref name="start"/>.
    /// </summary>
    Task<IReadOnlyList<LabeledCashEntry>> GetLabeledEntries(int userId, DateTime start, DateTime end,
        bool endInclusive = false, bool outflowsOnly = false, int? labelId = null, CancellationToken ct = default);

    /// <summary>The non-zero entries posted in [<paramref name="startInclusive"/>, <paramref name="endExclusive"/>), the input of recurring-payment detection.</summary>
    Task<IReadOnlyList<RecurringCashCandidate>> GetRecurringCandidates(int userId, DateTime startInclusive, DateTime endExclusive, CancellationToken ct = default);
}