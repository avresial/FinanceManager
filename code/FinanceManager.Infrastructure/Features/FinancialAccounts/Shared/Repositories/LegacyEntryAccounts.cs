using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

internal static class LegacyEntryAccounts
{
    /// <summary>
    /// Resolves the owner and currency of every linked account id. Entries without a link (<c>null</c>) belong to
    /// their stamped owner and are skipped. Returns null when a linked id is not an account of this kind.
    /// </summary>
    public static async Task<Dictionary<int, (int UserId, int CurrencyId)>?> Resolve(
        AppDbContext context, AccountType kind, IEnumerable<int?> accountIds, CancellationToken cancellationToken)
    {
        var ids = accountIds.OfType<int>().Distinct().ToArray();

        Dictionary<int, (int UserId, int CurrencyId)> accounts = [];
        foreach (var chunk in ids.Chunk(2_000))
        {
            var rows = await context.Accounts.Where(a => chunk.Contains(a.AccountId) && a.AccountType == kind && a.UserId != 0)
                .Select(a => new { a.AccountId, a.UserId, a.CurrencyId }).ToListAsync(cancellationToken);
            if (rows.Count != chunk.Length) return null;
            foreach (var row in rows)
                accounts.Add(row.AccountId, (row.UserId, row.CurrencyId));
        }
        return accounts;
    }
}