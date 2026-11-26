using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Features.Transactions.Shared;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;

public class CashTransactionRepository(AppDbContext context) : ICashTransactionReader
{
    public async Task<IReadOnlyList<CashLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        if (query.Take <= 0) return [];
        var entries = context.CurrencyEntries.AsNoTracking().Where(e => e.UserId == userId);
        if (query.Start is DateTime start) entries = entries.Where(e => e.PostingDate >= start);
        if (query.End is DateTime end) entries = entries.Where(e => e.PostingDate <= end);
        if (query.Boundary is OwnerHistoryBoundary cursor)
            entries = entries.Where(e => e.PostingDate < cursor.Date || e.PostingDate == cursor.Date &&
                (cursor.IncludeEqualId ? e.EntryId <= cursor.EntryId : e.EntryId < cursor.EntryId));

        var rows = entries.Select(e => new
        {
            Entry = e,
            Currency = context.Currencies.Where(c => c.Id == e.CurrencyId).Select(c => c.ShortName).FirstOrDefault()
        });
        if (query.NormalizedCurrencies() is { Count: > 0 } currencies)
            rows = rows.Where(x => x.Currency != null && currencies.Contains(x.Currency.ToUpper()));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.Entry.Description.ToUpper().Contains(term) ||
                x.Entry.ContractorDetails != null && x.Entry.ContractorDetails.ToUpper().Contains(term));
        }

        var page = await rows.OrderByDescending(x => x.Entry.PostingDate).ThenByDescending(x => x.Entry.EntryId)
            .Take(query.Take).Select(x => new CashLogEntry(x.Entry.EntryId, x.Entry.PostingDate,
                x.Entry.ValueChange, x.Entry.Description, x.Entry.CurrencyId, x.Currency, x.Entry.ContractorDetails, Array.Empty<FinancialLabel>())).ToListAsync(ct);
        if (!query.IncludeDetails || page.Count == 0) return page;

        var labels = await LabelsByEntry(userId, [.. page.Select(x => x.EntryId)], ct);
        return [.. page.Select(x => x with { Labels = labels.GetValueOrDefault(x.EntryId) ?? [] })];
    }

    public async Task<IReadOnlyList<CashLogEntry>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var ids = entryIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var rows = await context.CurrencyEntries.AsNoTracking()
            .Where(e => e.UserId == userId && ids.Contains(e.EntryId))
            .Select(e => new
            {
                Entry = e,
                Currency = context.Currencies.Where(c => c.Id == e.CurrencyId).Select(c => c.ShortName).FirstOrDefault()
            })
            .OrderByDescending(x => x.Entry.PostingDate).ThenByDescending(x => x.Entry.EntryId)
            .Select(x => new CashLogEntry(x.Entry.EntryId, x.Entry.PostingDate, x.Entry.ValueChange,
                x.Entry.Description, x.Entry.CurrencyId, x.Currency, x.Entry.ContractorDetails, Array.Empty<FinancialLabel>()))
            .ToListAsync(ct);
        if (rows.Count == 0) return rows;

        var labels = await LabelsByEntry(userId, [.. rows.Select(x => x.EntryId)], ct);
        return [.. rows.Select(x => x with { Labels = labels.GetValueOrDefault(x.EntryId) ?? [] })];
    }

    public async Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var currencyIds = context.CurrencyEntries.Where(e => e.UserId == userId).Select(e => e.CurrencyId);
        return await context.Currencies.AsNoTracking().Where(c => currencyIds.Contains(c.Id)).Select(c => c.ShortName).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OwnerLedgerPosition>> GetPositions(int userId, DateTime asOf, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var asOfUtc = OwnerLedgerTimestamps.ToUtc(asOf);
        return await Positions(OwnerEntries(userId).Where(e => e.PostingDate <= asOfUtc)).ToListAsync(ct);
    }

    public async Task<OwnerLedgerRange> GetValueRange(int userId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var startUtc = OwnerLedgerTimestamps.ToUtc(start);
        var endUtc = OwnerLedgerTimestamps.ToUtc(end);
        if (endUtc < startUtc) return OwnerLedgerRange.Empty;

        // Two plain sequential reads: the grouped balance before the range, then the range's deltas.
        var opening = await Positions(OwnerEntries(userId).Where(e => e.PostingDate < startUtc)).ToListAsync(ct);
        var points = await OwnerEntries(userId).Where(e => e.PostingDate >= startUtc && e.PostingDate <= endUtc)
            .OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId)
            .Select(e => new OwnerLedgerPoint(e.CurrencyId, e.EntryId, e.PostingDate, e.ValueChange)).ToListAsync(ct);
        return new OwnerLedgerRange(opening, points);
    }

    public async Task<bool> HasAnyEntries(int userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return await OwnerEntries(userId).AnyAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetLabelTotals(int userId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var startUtc = OwnerLedgerTimestamps.ToUtc(start);
        var endUtc = OwnerLedgerTimestamps.ToUtc(end);
        if (endUtc < startUtc) return new Dictionary<int, decimal>();

        var totals = await LabelLinks(EntriesBetween(userId, startUtc, endUtc, endInclusive: true))
            .GroupBy(x => x.LabelId).Select(g => new { LabelId = g.Key, Total = g.Sum(x => x.ValueChange) }).ToListAsync(ct);
        return totals.ToDictionary(x => x.LabelId, x => x.Total);
    }

    public async Task<IReadOnlyList<LabeledCashEntry>> GetLabeledEntries(int userId, DateTime start, DateTime end,
        bool endInclusive = false, bool outflowsOnly = false, int? labelId = null, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var startUtc = OwnerLedgerTimestamps.ToUtc(start);
        var endUtc = OwnerLedgerTimestamps.ToUtc(end);
        if (endInclusive ? endUtc < startUtc : endUtc <= startUtc) return [];

        var entries = EntriesBetween(userId, startUtc, endUtc, endInclusive);
        if (outflowsOnly) entries = entries.Where(e => e.ValueChange < 0);
        if (labelId is int label) entries = entries.Where(e => e.Labels.Any(l => l.Id == label));

        // Two reads over the same predicate (no id list to outgrow a parameter limit): the entries, then their label links.
        var rows = await entries.Select(e => new { e.EntryId, CurrencyId = e.CurrencyId, e.PostingDate, e.ValueChange }).ToListAsync(ct);
        if (rows.Count == 0) return [];

        var labelLinks = await LabelLinks(entries).Select(x => new { x.EntryId, x.LabelId }).ToListAsync(ct);
        var labelIdsByEntry = labelLinks.GroupBy(x => x.EntryId).ToDictionary(g => g.Key, g => (IReadOnlyList<int>)[.. g.Select(x => x.LabelId)]);
        return [.. rows.Select(r => new LabeledCashEntry(r.CurrencyId, r.PostingDate, r.ValueChange, labelIdsByEntry.GetValueOrDefault(r.EntryId) ?? []))];
    }

    public async Task<IReadOnlyList<RecurringCashCandidate>> GetRecurringCandidates(int userId, DateTime startInclusive, DateTime endExclusive, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var startUtc = OwnerLedgerTimestamps.ToUtc(startInclusive);
        var endUtc = OwnerLedgerTimestamps.ToUtc(endExclusive);
        if (endUtc <= startUtc) return [];

        return await EntriesBetween(userId, startUtc, endUtc, endInclusive: false).Where(e => e.ValueChange != 0)
            .Select(e => new RecurringCashCandidate(e.EntryId, e.PostingDate, e.ValueChange, e.Description, e.ContractorDetails)).ToListAsync(ct);
    }

    // The labels of the owner's given entries in a second, split query, so the history rows stay one flat read.
    private async Task<Dictionary<long, List<FinancialLabel>>> LabelsByEntry(int userId, long[] ids, CancellationToken ct) =>
        await context.CurrencyEntries.AsNoTracking().Where(e => e.UserId == userId && ids.Contains(e.EntryId))
            .AsSplitQuery().Select(e => new
            {
                e.EntryId,
                Labels = e.Labels.Select(l => new FinancialLabel
                {
                    Id = l.Id,
                    Name = l.Name,
                    Classifications = l.Classifications.ToList()
                }).ToList()
            }).ToDictionaryAsync(x => x.EntryId, x => x.Labels, ct);

    // The entries' label links as plain join-table rows: a join the database plans freely, where navigating the collection would need APPLY.
    private IQueryable<EntryLabelLink> LabelLinks(IQueryable<CashEntry> entries) =>
        context.Set<Dictionary<string, object>>("CurrencyAccountEntryFinancialLabel")
            .Join(entries, link => EF.Property<long>(link, "CurrencyAccountEntryEntryId"), entry => entry.EntryId,
                (link, entry) => new EntryLabelLink { EntryId = entry.EntryId, LabelId = EF.Property<int>(link, "LabelsId"), ValueChange = entry.ValueChange });

    private sealed class EntryLabelLink
    {
        public long EntryId { get; init; }
        public int LabelId { get; init; }
        public decimal ValueChange { get; init; }
    }

    private IQueryable<CashEntry> EntriesBetween(int userId, DateTime start, DateTime end, bool endInclusive)
    {
        var startUtc = OwnerLedgerTimestamps.ToUtc(start);
        var endUtc = OwnerLedgerTimestamps.ToUtc(end);
        var entries = OwnerEntries(userId).Where(e => e.PostingDate >= startUtc);
        return endInclusive ? entries.Where(e => e.PostingDate <= endUtc) : entries.Where(e => e.PostingDate < endUtc);
    }

    private IQueryable<CashEntry> OwnerEntries(int userId) => context.CurrencyEntries.AsNoTracking().Where(e => e.UserId == userId);

    private static IQueryable<OwnerLedgerPosition> Positions(IQueryable<CashEntry> entries) =>
        entries.GroupBy(e => e.CurrencyId).Select(g => new OwnerLedgerPosition(g.Key, g.Sum(e => e.ValueChange), g.Max(e => e.PostingDate)));
}