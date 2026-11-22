using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

internal static class LegacyEntryAccounts
{
    public static async Task<Dictionary<int, (int UserId, int CurrencyId)>?> Resolve(
        AppDbContext context, AccountType kind, IEnumerable<int?> accountIds, CancellationToken cancellationToken)
    {
        var ids = accountIds.Distinct().ToArray();
        if (ids.Contains(null)) return null;

        Dictionary<int, (int UserId, int CurrencyId)> accounts = [];
        foreach (var chunk in ids.Select(id => id!.Value).Chunk(2_000))
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