using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Bonds.Repositories;

public interface IBondTransactionReader
{
    Task<IReadOnlyList<BondLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default);

    /// <summary>Distinct short names of the currencies of the bonds the user has entries in.</summary>
    Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default);

    /// <summary>
    /// The pooled units per bond definition (sum of <c>ValueChange</c>, linked to an account or not) of entries posted on or before <paramref name="asOf"/>,
    /// with the posting date of the latest entry, which is where interest accrual restarts.
    /// </summary>
    Task<IReadOnlyList<OwnerLedgerPosition>> GetPositions(int userId, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// The pooled units per bond definition before <paramref name="start"/> plus every entry in [<paramref name="start"/>, <paramref name="end"/>]
    /// ordered by posting date, then entry id. Returns an empty range when <paramref name="end"/> is before <paramref name="start"/>.
    /// </summary>
    Task<OwnerLedgerRange> GetValueRange(int userId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>True when the user has at least one bond entry, linked to an account or not.</summary>
    Task<bool> HasAnyEntries(int userId, CancellationToken ct = default);
}