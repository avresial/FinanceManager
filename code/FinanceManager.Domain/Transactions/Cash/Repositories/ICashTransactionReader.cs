using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Cash.Repositories;

public interface ICashTransactionReader
{
    Task<IReadOnlyList<CashLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default);

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
}