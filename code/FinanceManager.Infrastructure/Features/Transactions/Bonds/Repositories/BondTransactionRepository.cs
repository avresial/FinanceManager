using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Features.Transactions.Shared;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;

public class BondTransactionRepository(AppDbContext context) : IBondTransactionReader
{
    public async Task<IReadOnlyList<BondLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        if (query.Take <= 0) return [];
        var entries = context.BondEntries.AsNoTracking().Where(e => e.UserId == userId);
        if (query.Start is DateTime start) entries = entries.Where(e => e.PostingDate >= start);
        if (query.End is DateTime end) entries = entries.Where(e => e.PostingDate <= end);
        if (query.Boundary is OwnerHistoryBoundary cursor)
            entries = entries.Where(e => e.PostingDate < cursor.Date || e.PostingDate == cursor.Date &&
                (cursor.IncludeEqualId ? e.EntryId <= cursor.EntryId : e.EntryId < cursor.EntryId));

        // A bond definition that is missing keeps its entry visible, just without a currency or name.
        var rows = from e in entries
                   join b in context.Bonds on e.BondDetailsId equals b.Id into bonds
                   from b in bonds.DefaultIfEmpty()
                   select new { Entry = e, Currency = b != null ? b.Currency.ShortName : null, BondName = b != null ? b.Name : null };
        if (query.NormalizedCurrencies() is { Count: > 0 } currencies)
            rows = rows.Where(x => x.Currency != null && currencies.Contains(x.Currency.ToUpper()));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.Entry.Description.ToUpper().Contains(term) ||
                x.BondName != null && x.BondName.ToUpper().Contains(term));
        }

        var page = await rows.OrderByDescending(x => x.Entry.PostingDate).ThenByDescending(x => x.Entry.EntryId)
            .Take(query.Take).Select(x => new BondLogEntry(x.Entry.AccountId, x.Entry.EntryId, x.Entry.PostingDate,
                x.Entry.ValueChange, x.Entry.Description, x.Entry.BondDetailsId, x.Currency, x.BondName, Array.Empty<FinancialLabel>())).ToListAsync(ct);
        if (!query.IncludeDetails || page.Count == 0) return page;

        var ids = page.Select(x => x.EntryId).ToArray();
        var labels = await context.BondEntries.AsNoTracking().Where(e => e.UserId == userId && ids.Contains(e.EntryId))
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
        return [.. page.Select(x => x with { Labels = labels.GetValueOrDefault(x.EntryId) ?? [] })];
    }

    public async Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var bondIds = context.BondEntries.Where(e => e.UserId == userId).Select(e => e.BondDetailsId);
        return await context.Bonds.AsNoTracking().Where(b => bondIds.Contains(b.Id)).Select(b => b.Currency.ShortName).Distinct().ToListAsync(ct);
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
            .Select(e => new OwnerLedgerPoint(e.BondDetailsId, e.EntryId, e.PostingDate, e.ValueChange)).ToListAsync(ct);
        return new OwnerLedgerRange(opening, points);
    }

    private IQueryable<BondAccountEntry> OwnerEntries(int userId) => context.BondEntries.AsNoTracking().Where(e => e.UserId == userId);

    private static IQueryable<OwnerLedgerPosition> Positions(IQueryable<BondAccountEntry> entries) =>
        entries.GroupBy(e => e.BondDetailsId).Select(g => new OwnerLedgerPosition(g.Key, g.Sum(e => e.ValueChange), g.Max(e => e.PostingDate)));
}