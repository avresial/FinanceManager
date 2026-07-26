using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;

internal sealed class BondEntryValueCalculator(AppDbContext context)
{
    public async Task Recalculate(int accountId, DateTime startDate)
    {
        // Each supported provider gets its own dialect below; anything else falls back to the managed
        // loop rather than being handed SQL Server syntax it cannot parse.
        if (!context.Database.IsRelational() || !DatabaseProviders.HasSetBasedRecalculation(context))
        {
            await RecalculateInMemory(accountId, startDate);
            return;
        }

        if (DatabaseProviders.IsSqlite(context))
        {
            // SQLite has no DISTINCT ON, so the per-bond anchor is picked with ROW_NUMBER as on SQL
            // Server; the UPDATE names its target table rather than aliasing it, as SQLite requires.
            await context.Database.ExecuteSqlAsync($"""
                WITH anchors_raw AS (
                    SELECT "BondDetailsId", "Value" AS "AnchorValue",
                           ROW_NUMBER() OVER (PARTITION BY "BondDetailsId" ORDER BY "PostingDate" DESC, "EntryId" DESC) AS "rn"
                    FROM "BondEntries"
                    WHERE "AccountId" = {accountId} AND "PostingDate" < {startDate}
                ),
                anchors AS (
                    SELECT "BondDetailsId", "AnchorValue" FROM anchors_raw WHERE "rn" = 1
                ),
                running AS (
                    SELECT e."EntryId",
                           COALESCE(a."AnchorValue", 0) + SUM(e."ValueChange") OVER (
                               PARTITION BY e."BondDetailsId"
                               ORDER BY e."PostingDate", e."EntryId"
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS "NewValue"
                    FROM "BondEntries" e
                    LEFT JOIN anchors a ON a."BondDetailsId" = e."BondDetailsId"
                    WHERE e."AccountId" = {accountId} AND e."PostingDate" >= {startDate}
                )
                UPDATE "BondEntries"
                SET "Value" = r."NewValue"
                FROM running AS r
                WHERE "BondEntries"."EntryId" = r."EntryId"
                """);
        }
        else if (DatabaseProviders.IsNpgsql(context))
        {
            await context.Database.ExecuteSqlAsync($"""
                WITH anchors AS (
                    SELECT DISTINCT ON ("BondDetailsId") "BondDetailsId", "Value" AS "AnchorValue"
                    FROM "BondEntries"
                    WHERE "AccountId" = {accountId} AND "PostingDate" < {startDate}
                    ORDER BY "BondDetailsId", "PostingDate" DESC, "EntryId" DESC
                ),
                running AS (
                    SELECT e."EntryId",
                           COALESCE(a."AnchorValue", 0) + SUM(e."ValueChange") OVER (
                               PARTITION BY e."BondDetailsId"
                               ORDER BY e."PostingDate", e."EntryId"
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS "NewValue"
                    FROM "BondEntries" e
                    LEFT JOIN anchors a ON a."BondDetailsId" = e."BondDetailsId"
                    WHERE e."AccountId" = {accountId} AND e."PostingDate" >= {startDate}
                )
                UPDATE "BondEntries" AS e
                SET "Value" = r."NewValue"
                FROM running AS r
                WHERE e."EntryId" = r."EntryId"
                """);
        }
        else
        {
            await context.Database.ExecuteSqlAsync($"""
                WITH anchors_raw AS (
                    SELECT BondDetailsId, Value AS AnchorValue,
                           ROW_NUMBER() OVER (PARTITION BY BondDetailsId ORDER BY PostingDate DESC, EntryId DESC) AS rn
                    FROM BondEntries
                    WHERE AccountId = {accountId} AND PostingDate < {startDate}
                ),
                anchors AS (
                    SELECT BondDetailsId, AnchorValue FROM anchors_raw WHERE rn = 1
                ),
                running AS (
                    SELECT e.EntryId,
                           COALESCE(a.AnchorValue, 0) + SUM(e.ValueChange) OVER (
                               PARTITION BY e.BondDetailsId
                               ORDER BY e.PostingDate, e.EntryId
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                           ) AS NewValue
                    FROM BondEntries e
                    LEFT JOIN anchors a ON a.BondDetailsId = e.BondDetailsId
                    WHERE e.AccountId = {accountId} AND e.PostingDate >= {startDate}
                )
                UPDATE e
                SET Value = r.NewValue
                FROM BondEntries e
                INNER JOIN running r ON e.EntryId = r.EntryId
                """);
        }
    }

    private async Task RecalculateInMemory(int accountId, DateTime startDate)
    {
        var entries = await context.BondEntries
            .Where(e => e.AccountId == accountId && e.PostingDate >= startDate)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.EntryId)
            .ToListAsync();

        if (entries.Count == 0) return;

        var bondIds = entries.Select(e => e.BondDetailsId).Distinct().ToList();
        var anchors = (await context.BondEntries
                .AsNoTracking()
                .Where(e => e.AccountId == accountId && bondIds.Contains(e.BondDetailsId) && e.PostingDate < startDate)
                .OrderByDescending(e => e.PostingDate)
                .ThenByDescending(e => e.EntryId)
                .ToListAsync())
            .GroupBy(e => e.BondDetailsId)
            .ToDictionary(g => g.Key, g => g.First().Value);

        foreach (var bondGroup in entries.GroupBy(e => e.BondDetailsId))
        {
            decimal running = anchors.TryGetValue(bondGroup.Key, out var anchor) ? anchor : 0m;
            foreach (var entry in bondGroup.OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId))
            {
                running += entry.ValueChange;
                entry.Value = running;
            }
        }

        await context.SaveChangesAsync();
    }
}