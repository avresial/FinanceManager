using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Tests.Unit.Application.Services;

/// <summary>
/// An owner ledger held in memory that answers the owner readers the way the database does: pooled sums per partition
/// (currency for cash, bond definition for bonds) before the range plus the ordered entries inside it, and the labelled /
/// recurring-candidate views of the cash entries. Not a mock: it lets service tests assert on values instead of on how the readers were called.
/// </summary>
internal sealed class InMemoryOwnerLedger : ICashTransactionReader, IBondTransactionReader
{
    private sealed record Row(int PartitionId, long EntryId, DateTime PostingDate, decimal ValueChange,
        IReadOnlyList<int> LabelIds, string Description, string? ContractorDetails, int? AccountId);

    private readonly List<Row> _rows = [];
    private long _nextEntryId = 1;

    public InMemoryOwnerLedger Add(int partitionId, DateTime postingDate, decimal valueChange) =>
        Add(partitionId, postingDate, valueChange, [], string.Empty);

    public InMemoryOwnerLedger Add(int partitionId, DateTime postingDate, decimal valueChange, IReadOnlyList<int> labelIds,
        string description = "", string? contractorDetails = null, int? accountId = null)
    {
        _rows.Add(new Row(partitionId, _nextEntryId++, postingDate, valueChange, labelIds, description, contractorDetails, accountId));
        return this;
    }

    public Task<bool> HasAnyEntries(int userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return Task.FromResult(_rows.Count > 0);
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

    public Task<IReadOnlyDictionary<int, decimal>> GetLabelTotals(int userId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        IReadOnlyDictionary<int, decimal> totals = _rows.Where(r => r.PostingDate >= start && r.PostingDate <= end)
            .SelectMany(r => r.LabelIds.Select(labelId => (labelId, r.ValueChange)))
            .GroupBy(x => x.labelId).ToDictionary(g => g.Key, g => g.Sum(x => x.ValueChange));
        return Task.FromResult(totals);
    }

    public Task<IReadOnlyList<LabeledCashEntry>> GetLabeledEntries(int userId, DateTime start, DateTime end,
        bool endInclusive = false, bool outflowsOnly = false, int? labelId = null, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        IReadOnlyList<LabeledCashEntry> entries = [.. _rows
            .Where(r => r.PostingDate >= start && (endInclusive ? r.PostingDate <= end : r.PostingDate < end))
            .Where(r => !outflowsOnly || r.ValueChange < 0)
            .Where(r => labelId is not int label || r.LabelIds.Contains(label))
            .OrderBy(r => r.PostingDate).ThenBy(r => r.EntryId)
            .Select(r => new LabeledCashEntry(r.PartitionId, r.PostingDate, r.ValueChange, r.LabelIds))];
        return Task.FromResult(entries);
    }

    public Task<IReadOnlyList<RecurringCashCandidate>> GetRecurringCandidates(int userId, DateTime startInclusive, DateTime endExclusive, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        IReadOnlyList<RecurringCashCandidate> candidates = [.. _rows
            .Where(r => r.PostingDate >= startInclusive && r.PostingDate < endExclusive && r.ValueChange != 0)
            .Select(r => new RecurringCashCandidate(r.EntryId, r.PostingDate, r.ValueChange, r.Description, r.ContractorDetails))];
        return Task.FromResult(candidates);
    }

    Task<IReadOnlyList<CashLogEntry>> ICashTransactionReader.GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<BondLogEntry?> IBondTransactionReader.GetById(int userId, long entryId, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default) => throw new NotSupportedException();

    Task<IReadOnlyList<CashLogEntry>> ICashTransactionReader.GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<IReadOnlyList<BondLogEntry>> IBondTransactionReader.GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct) =>
        throw new NotSupportedException();

    private static IReadOnlyList<OwnerLedgerPosition> Positions(IEnumerable<Row> rows) =>
        [.. rows.GroupBy(r => r.PartitionId).OrderBy(g => g.Key)
            .Select(g => new OwnerLedgerPosition(g.Key, g.Sum(r => r.ValueChange), g.Max(r => r.PostingDate)))];
}