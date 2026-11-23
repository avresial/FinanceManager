using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Shared;

/// <summary>
/// The single running-balance walk over an <see cref="OwnerLedgerRange"/>. It answers "what was each partition's pooled
/// balance (and latest posting date) at this instant" for cash and bonds alike, so no service re-implements the opening +
/// delta arithmetic. Balances are computed once per partition as prefix sums, then read by binary search in any order.
/// </summary>
public sealed class OwnerLedgerWalk
{
    private readonly Dictionary<int, PartitionLedger> _partitions = [];

    // A conventional constructor: the per-partition prefix sums are built from the range's grouped points, not just assigned.
    public OwnerLedgerWalk(OwnerLedgerRange range)
    {
        foreach (var opening in range.Opening)
            _partitions[opening.PartitionId] = new PartitionLedger(opening);

        foreach (var group in range.Points.GroupBy(p => p.PartitionId))
        {
            if (!_partitions.TryGetValue(group.Key, out var ledger))
                _partitions[group.Key] = ledger = new PartitionLedger(null);
            ledger.Add(group.OrderBy(p => p.PostingDate).ThenBy(p => p.EntryId));
        }
    }

    /// <summary>The ids of every partition that has an opening balance or an entry in the range.</summary>
    public IReadOnlyCollection<int> PartitionIds => _partitions.Keys;

    /// <summary>True when the owner has no opening balance and no entry in the range, so every series is empty.</summary>
    public bool IsEmpty => _partitions.Count == 0;

    /// <summary>
    /// The pooled position of every partition that has at least one entry on or before <paramref name="instant"/>,
    /// ordered by partition id. Partitions that net to zero are kept; callers decide whether zero matters.
    /// </summary>
    public IReadOnlyList<OwnerLedgerPosition> At(DateTime instant)
    {
        // Posting dates are UTC and DateTime comparison ignores Kind, so a local instant is converted first.
        if (instant.Kind == DateTimeKind.Local) instant = instant.ToUniversalTime();
        List<OwnerLedgerPosition> positions = [];
        foreach (var (partitionId, ledger) in _partitions.OrderBy(p => p.Key))
            if (ledger.At(partitionId, instant) is OwnerLedgerPosition position)
                positions.Add(position);
        return positions;
    }

    /// <summary>
    /// One entry per calendar day in [start.Date, end.Date], ascending, valued at the end of that day
    /// (entries posted on or before the day count).
    /// </summary>
    public IEnumerable<(DateTime Day, IReadOnlyList<OwnerLedgerPosition> Positions)> EachDayEnd(DateTime start, DateTime end)
    {
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            yield return (day, At(day.AddDays(1).AddTicks(-1)));
    }

    private sealed class PartitionLedger(OwnerLedgerPosition? opening)
    {
        private readonly List<DateTime> _postingDates = [];
        private readonly List<decimal> _balances = [];
        private readonly decimal _openingValue = opening?.Value ?? 0m;

        public void Add(IEnumerable<OwnerLedgerPoint> orderedPoints)
        {
            var balance = _openingValue;
            foreach (var point in orderedPoints)
            {
                balance += point.ValueChange;
                _postingDates.Add(point.PostingDate);
                _balances.Add(balance);
            }
        }

        public OwnerLedgerPosition? At(int partitionId, DateTime instant)
        {
            // Index of the last point posted on or before the instant.
            var low = 0;
            var high = _postingDates.Count;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (_postingDates[middle] <= instant) low = middle + 1;
                else high = middle;
            }

            if (low > 0) return new OwnerLedgerPosition(partitionId, _balances[low - 1], _postingDates[low - 1]);
            return opening is null ? null : new OwnerLedgerPosition(partitionId, opening.Value, opening.LastPostingDate);
        }
    }
}