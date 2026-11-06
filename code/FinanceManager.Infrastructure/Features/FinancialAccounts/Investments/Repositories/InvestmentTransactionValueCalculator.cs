using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

/// <summary>
/// Maintains <see cref="InvestmentTransaction.Value"/>: the units of a listing held in an account immediately after
/// each trade. A holding is the running sum of <see cref="InvestmentTransaction.SignedQuantity"/> per
/// (account, listing), ordered by posting date then entry id. Negative holdings (oversells) are stored as they are.
/// <para>
/// It loads one partition at a time and lets the change tracker write only the rows whose value changed, so it
/// behaves the same on every provider. The caller saves the changes, in the same transaction as the trade change.
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
            var rows = await context.InvestmentTransactions
                .Where(x => x.AccountId == accountId && x.AssetListingId == listingId)
                .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
                .ToListAsync(cancellationToken);

            var held = 0m;
            foreach (var row in rows)
            {
                held += row.SignedQuantity;
                if (row.Value == held) continue;
                row.Value = held;
                changed++;
            }
        }

        return changed;
    }
}