using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;

public class BondTransactionRepository(AppDbContext context) : IBondTransactionReader
{
    public async Task<IReadOnlyList<BondLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var entries = context.BondEntries.AsNoTracking().Where(e => e.UserId == userId);
        if (query.Start is DateTime start) entries = entries.Where(e => e.PostingDate >= start);
        if (query.End is DateTime end) entries = entries.Where(e => e.PostingDate <= end);
        if (query.Boundary is OwnerHistoryBoundary cursor)
            entries = entries.Where(e => e.PostingDate < cursor.Date || e.PostingDate == cursor.Date &&
                (cursor.IncludeEqualId ? e.EntryId <= cursor.EntryId : e.EntryId < cursor.EntryId));

        var rows = entries.Select(e => new
        {
            Entry = e,
            Currency = context.Bonds.Where(b => b.Id == e.BondDetailsId).Select(b => b.Currency.ShortName).FirstOrDefault(),
            BondName = context.Bonds.Where(b => b.Id == e.BondDetailsId).Select(b => b.Name).FirstOrDefault()
        });
        if (query.Currencies is { Count: > 0 } currencies)
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
}