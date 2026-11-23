# EF Core migrations

[Architecture index](../README.md) · [Owning arc42 section](../07-deployment-view.md)

Commands in this guide run from the repository root unless an explicit `cd` is shown.

```powershell
dotnet tool install dotnet-ef -g
dotnet tool update --global dotnet-ef

dotnet ef migrations add <MigrationName> `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj

dotnet ef database update `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj

dotnet ef migrations add <AnotherMigrationName> `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj
```

## Production rollback and backup

Production rollback is documented in [database recovery runbook](database-recovery.md).

Use that runbook for:
- manual Supabase backups before deployment
- restoring from Supabase backups or manual logical dumps
- reverting production to a previous EF Core migration

## Transaction owner keys

The expand migration adds nullable ownership columns for cash and bonds and a nullable cash currency. Its two backfill statements copy keys only from accounts of the matching kind with a nonzero owner. Existing trades and dividends retain their stored owners. Account-based reads continue during rollout.

Dev CI applies migrations before replacing the API. During that interval an old writer can leave cash or bond keys empty, and an old investment-account delete can fail if dividends still reference it. Retry a failed delete after deployment. Before enabling owner reads and again during the #1138 maintenance window, rerun the two provider-specific UPDATE statements from [ExpandTransactionOwnerKeys](../../../code/FinanceManager.Api/Migrations/20261122201026_ExpandTransactionOwnerKeys.cs) and run the inventory below. During cutover, take a backup and stop writers first. The updates can be repeated. Resolve discrepancies before exposing the affected rows or enforcing ownership. Confirm a normal cash and bond write persists the expected keys.

Reverse the expand migration only while every ledger row still has an account link. The migration checks all four tables before restoring the required columns and cascading dividend foreign key. Use the backup recovery procedure after account-free writes begin.

### Data-quality inventory

Run the following read-only batch on the expanded database before enabling owner reads, after an expand deployment, and again before the #1138 cutover. Use a consistent snapshot, or drain writers for the final inventory. SQL Server sessions need `SET QUOTED_IDENTIFIER ON;` and the usual default schema; PostgreSQL uses the ordinary search path. The first result set includes every anomalous row once and preserves the IDs needed for adjudication.

~~~sql
WITH entries AS (
    SELECT 'cash' AS kind, 0 AS account_type, "EntryId" AS entry_id,
           "AccountId" AS account_id, "UserId" AS owner_id, "CurrencyId" AS currency_id
    FROM "CurrencyEntries"
    UNION ALL
    SELECT 'bond', 2, "EntryId", "AccountId", "UserId", NULL FROM "BondEntries"
    UNION ALL
    SELECT 'trade', 1, "Id", "AccountId", "UserId", NULL FROM "InvestmentTransactions"
    UNION ALL
    SELECT 'dividend', 1, "Id", "AccountId", "UserId", NULL FROM "DividendPayments"
)
SELECT e.*, a."AccountType" AS actual_account_type, a."UserId" AS account_owner_id,
       a."CurrencyId" AS account_currency_id, c."ShortName" AS currency_code,
       CASE WHEN e.account_id IS NULL THEN 1 ELSE 0 END AS unlinked,
       CASE WHEN e.account_id IS NOT NULL AND a."AccountId" IS NULL THEN 1 ELSE 0 END AS missing_account,
       CASE WHEN e.owner_id > 0 AND u."Id" IS NULL THEN 1 ELSE 0 END AS missing_user
FROM entries AS e
LEFT JOIN "Accounts" AS a ON a."AccountId" = e.account_id
LEFT JOIN "Currencies" AS c ON c."Id" = e.currency_id
LEFT JOIN "Users" AS u ON u."Id" = e.owner_id
WHERE a."AccountId" IS NULL OR a."AccountType" <> e.account_type
   OR a."UserId" = 0 OR e.owner_id IS NULL OR e.owner_id = 0 OR e.owner_id <> a."UserId"
   OR (e.owner_id > 0 AND u."Id" IS NULL)
   OR (e.kind = 'cash' AND (c."Id" IS NULL OR e.currency_id <> a."CurrencyId"
                           OR (e.currency_id = 0 AND UPPER(c."ShortName") <> 'PLN')))
ORDER BY e.kind, e.entry_id;

SELECT "Id", "ShortName", "Symbol"
FROM "Currencies"
WHERE "Id" = 0 OR UPPER("ShortName") = 'PLN'
ORDER BY "Id";

SELECT COUNT(*) AS legacy_cash_entries_on_id_zero
FROM "CurrencyEntries" AS e
JOIN "Accounts" AS a ON a."AccountId" = e."AccountId"
WHERE a."AccountType" = 0 AND a."CurrencyId" = 0;
~~~

- For a pre-expand run of the first result set, replace the cash and bond `"UserId"` references in the CTE with a correlated lookup of `Accounts.UserId` by the existing `AccountId`, and replace the cash `"CurrencyId"` reference with the same lookup of `Accounts.CurrencyId`. Use that form only to inventory historical relationships. It cannot detect missing expanded values. The other two statements work on either schema.
- If legacy cash references ID 0, require exactly the row `(0, PLN)` and no conflicting PLN row. An empty schema may have no currencies before lazy defaults are created. Where referenced ID 0 and PLN are both absent, repair using the existing default-currency path before enabling owner reads. An occupied ID 0 or PLN at another ID requires explicit reconciliation. Do not substitute a different currency.
- Step 2 deletes trades and dividends with a non-null account link that is missing or belongs to a non-stock account. It preserves null links and leaves owner mismatches for adjudication. Run the inventory and retain a backup before deployment. Rolling the indexes back cannot restore deleted rows. Investment-account deletion now removes its linked trades and dividends together.

## Transaction rule account conditions

`RetireTransactionRuleAccountConditions` is a data-only migration (no schema or snapshot change) that removes every `account` condition from `TransactionRules.ConditionsJson` and sets `IsEnabled = false` on each affected rule. It matches the type case-insensitively after trimming, keeps the other conditions in order, leaves untouched rules and their timestamps alone and changes nothing on a second run. `Down` is a no-op: the removed account ids and the previous enabled flags cannot be rebuilt, so a rollback restores the pre-deploy backup. Exotic padding or key casing that the SQL does not recognise cannot broaden a rule during the dev window between migration and deploy: the runtime builds such a rule disabled (logged once). After the production maintenance window, no rule should still contain an `account` condition: `SELECT COUNT(*) FROM "TransactionRules" WHERE "ConditionsJson" ILIKE '%"account"%'` should be 0.


## Alert currency scope

[MigrateAlertsToCurrencyScope](../../../code/FinanceManager.Api/Migrations/20261123162630_MigrateAlertsToCurrencyScope.cs) adds the nullable `FinancialAlerts.CurrencyId`, copies it only from the alert's own Currency account of the same user, disables all four cash alert types (balance, category, merchant, large transaction) with their trigger state reset, and clears `AccountId`. Subscription alerts stay enabled. It changes data only for rows that still need it, so a second run changes nothing, and it cannot fail on missing or foreign accounts. Reverting drops only the column; the disabled flags and cleared scopes need a backup restore. The column `AccountId` stays until the contract step.
