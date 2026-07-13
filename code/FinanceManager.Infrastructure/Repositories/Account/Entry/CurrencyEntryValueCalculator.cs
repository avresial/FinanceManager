using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

internal sealed class CurrencyEntryValueCalculator(AppDbContext context)
{
    public async Task Recalculate(int accountId, DateTime startDate)
    {
        var anchor = await context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId && e.PostingDate < startDate)
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .Select(e => (decimal?)e.Value)
            .FirstOrDefaultAsync();

        if (!context.Database.IsRelational())
        {
            var entries = await context.CurrencyEntries
                .Where(e => e.AccountId == accountId && e.PostingDate >= startDate)
                .OrderBy(e => e.PostingDate)
                .ThenBy(e => e.EntryId)
                .ToListAsync();

            decimal running = anchor ?? 0m;
            foreach (var entry in entries)
            {
                running += entry.ValueChange;
                entry.Value = running;
            }

            await context.SaveChangesAsync();
            return;
        }

        if (context.Database.ProviderName?.StartsWith("Npgsql") == true)
        {
            await context.Database.ExecuteSqlAsync($"""
                WITH running AS (
                    SELECT "EntryId",
                           {anchor ?? 0m} + SUM("ValueChange") OVER (
                               ORDER BY "PostingDate", "EntryId"
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS "NewValue"
                    FROM "CurrencyEntries"
                    WHERE "AccountId" = {accountId}
                      AND "PostingDate" >= {startDate}
                )
                UPDATE "CurrencyEntries" AS e
                SET "Value" = r."NewValue"
                FROM running AS r
                WHERE e."EntryId" = r."EntryId"
                """);
        }
        else
        {
            await context.Database.ExecuteSqlAsync($"""
                WITH running AS (
                    SELECT EntryId,
                           {anchor ?? 0m} + SUM(ValueChange) OVER (
                               ORDER BY PostingDate, EntryId
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS NewValue
                    FROM CurrencyEntries
                    WHERE AccountId = {accountId}
                      AND PostingDate >= {startDate}
                )
                UPDATE e
                SET Value = r.NewValue
                FROM CurrencyEntries AS e
                INNER JOIN running AS r ON e.EntryId = r.EntryId
                """);
        }
    }
}