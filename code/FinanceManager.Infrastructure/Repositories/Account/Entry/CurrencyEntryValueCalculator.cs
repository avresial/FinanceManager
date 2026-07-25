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

        // Each supported provider gets its own dialect below; anything else falls back to the managed
        // loop rather than being handed SQL Server syntax it cannot parse.
        if (!context.Database.IsRelational() || !DatabaseProviders.HasSetBasedRecalculation(context))
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

        if (DatabaseProviders.IsSqlite(context))
        {
            // SQLite supports window functions and UPDATE ... FROM, but not SQL Server's
            // "UPDATE <alias> ... FROM <table> AS <alias>" form: the target is named, not aliased.
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
                UPDATE "CurrencyEntries"
                SET "Value" = r."NewValue"
                FROM running AS r
                WHERE "CurrencyEntries"."EntryId" = r."EntryId"
                """);
        }
        else if (DatabaseProviders.IsNpgsql(context))
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