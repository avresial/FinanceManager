using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Tests.Unit.Application.Services;

/// <summary>
/// An owner ledger held in memory that answers the owner readers the way the database does: pooled sums per partition
/// (currency for cash, bond definition for bonds) before the range plus the ordered entries inside it.
/// Not a mock: it lets service tests assert on values instead of on how the readers were called.
/// </summary>
internal sealed class InMemoryOwnerLedger : ICashTransactionReader, IBondTransactionReader
{
    private readonly List<(int PartitionId, long EntryId, DateTime PostingDate, decimal ValueChange)> _rows = [];
    private long _nextEntryId = 1;

    public InMemoryOwnerLedger Add(int partitionId, DateTime postingDate, decimal valueChange)
    {
        _rows.Add((partitionId, _nextEntryId++, postingDate, valueChange));
        return this;
    }

    public Task<IReadOnlyList<OwnerLedgerPosition>> GetPositions(int userId, DateTime asOf, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return Task.FromResult(Positions(_rows.Where(r => r.PostingDate <= asOf)));
    }

    public Task<OwnerLedgerRange> GetValueRange(int userId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        if (end < start) return Task.FromResult(OwnerLedgerRange.Empty);

        var points = _rows.Where(r => r.PostingDate >= start && r.PostingDate <= end)
            .OrderBy(r => r.PostingDate).ThenBy(r => r.EntryId)
            .Select(r => new OwnerLedgerPoint(r.PartitionId, r.EntryId, r.PostingDate, r.ValueChange)).ToList();
        return Task.FromResult(new OwnerLedgerRange(Positions(_rows.Where(r => r.PostingDate < start)), points));
    }

    public Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default) => throw new NotSupportedException();

    Task<IReadOnlyList<CashLogEntry>> ICashTransactionReader.GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<IReadOnlyList<BondLogEntry>> IBondTransactionReader.GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct) =>
        throw new NotSupportedException();

    private static IReadOnlyList<OwnerLedgerPosition> Positions(IEnumerable<(int PartitionId, long EntryId, DateTime PostingDate, decimal ValueChange)> rows) =>
        [.. rows.GroupBy(r => r.PartitionId).OrderBy(g => g.Key)
            .Select(g => new OwnerLedgerPosition(g.Key, g.Sum(r => r.ValueChange), g.Max(r => r.PostingDate)))];
}