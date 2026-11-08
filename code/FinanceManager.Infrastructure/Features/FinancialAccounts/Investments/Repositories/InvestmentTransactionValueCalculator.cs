using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

/// <summary>
/// Maintains <see cref="InvestmentTransaction.Value"/>: the units of a listing held in an account immediately after
/// each trade. A holding is the running sum of <see cref="InvestmentTransaction.SignedQuantity"/> per
/// (account, listing), ordered by posting date then entry id. Negative holdings (oversells) are stored as they are.
/// <para>
/// It reads each partition's stored quantities, not tracked entities, because a tracked entity can hold a stale value
/// or more decimal places than the column stores. Only rows whose stored value is wrong are loaded and marked as
/// changed, so it behaves the same on every provider. The caller saves the changes, in the same transaction as the
/// trade change.
/// </para>
/// </summary>
internal sealed class InvestmentTransactionValueCalculator(AppDbContext context)
{
    /// <summary>Recomputes every partition once and returns how many rows changed.</summary>
    public async Task<int> Recalculate(IEnumerable<(int AccountId, long AssetListingId)> partitions, CancellationToken cancellationToken = default)
    {
        var changed = 0;
        foreach (var (accountId, listingId) in partitions.Distinct())
        {
            var rows = await context.InvestmentTransactions.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.AssetListingId == listingId)
                .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
                .Select(x => new { x.EntryId, x.Type, x.Quantity, x.Value })
                .ToListAsync(cancellationToken);

            var held = 0m;
            Dictionary<long, decimal> corrections = [];
            foreach (var row in rows)
            {
                held += new InvestmentTransaction { Type = row.Type, Quantity = row.Quantity }.SignedQuantity;
                if (row.Value != held)
                    corrections[row.EntryId] = held;
            }

            if (corrections.Count == 0)
                continue;

            var ids = corrections.Keys.ToArray();
            var entities = await context.InvestmentTransactions.Where(x => ids.Contains(x.EntryId)).ToListAsync(cancellationToken);
            foreach (var entity in entities)
            {
                entity.Value = corrections[entity.EntryId];
                // A tracked entity may already hold this value while the stored row differs, so force the write.
                context.Entry(entity).Property(x => x.Value).IsModified = true;
                changed++;
            }
        }

        return changed;
    }
}