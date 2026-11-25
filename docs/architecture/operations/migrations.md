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

During earlier dev deployments, account writers could leave cash or bond keys empty between migration and API replacement. The cutover migration catches up those rows during the maintenance window below.

Reverse the expand migration only while every ledger row still has an account link. The migration checks all four tables before restoring the required columns and cascading dividend foreign key. Use the backup recovery procedure after account-free writes begin.

### Cutover inventory and maintenance window (#1138)

The deployment jobs for `main` (production) and `develop` (dev) apply pending EF migrations **before** deploying the published API. Stop all writers for this promotion. The catch-up in `EnforceTransactionOwnerKeys` fills only missing keys from a linked account of the correct kind with a nonzero owner. It preserves already stamped keys for comparison. [CI workflow](../../../.github/workflows/ci.yml) · [Cutover migration](../../../code/FinanceManager.Api/Migrations/20261124013806_EnforceTransactionOwnerKeys.cs)

**Inventory.** Check the last applied migration in `__EFMigrationsHistory` first. On the expanded schema, run the following read-only queries on a consistent snapshot. SQL Server sessions need `SET QUOTED_IDENTIFIER ON;`. The first query projects the same catch-up the migration will perform. It returns the affected ledger IDs and separate flags so an operator can identify each discrepancy. Positive and negative owners are both valid. Rows with complete owner keys and a null account link are expected.

```sql
WITH entries(kind, account_type, entry_id, account_id, stored_owner, stored_currency) AS (
    SELECT 'cash', 0, "EntryId", "AccountId", CAST("UserId" AS bigint), "CurrencyId" FROM "CurrencyEntries"
    UNION ALL SELECT 'bond', 2, "EntryId", "AccountId", CAST("UserId" AS bigint), NULL FROM "BondEntries"
    UNION ALL SELECT 'trade', 1, "Id", "AccountId", CAST("UserId" AS bigint), NULL FROM "InvestmentTransactions"
    UNION ALL SELECT 'dividend', 1, "Id", "AccountId", CAST("UserId" AS bigint), NULL FROM "DividendPayments"
), projected AS (
    SELECT e.*, a."AccountId" AS linked_id, a."AccountType" AS linked_type,
           a."UserId" AS linked_owner, a."CurrencyId" AS linked_currency,
           COALESCE(stored_owner, CASE WHEN kind IN ('cash','bond') AND a."AccountType" = account_type
                                               AND a."UserId" <> 0 THEN a."UserId" END) AS owner_id,
           COALESCE(stored_currency, CASE WHEN kind = 'cash' AND a."AccountType" = 0
                                               AND a."UserId" <> 0 THEN a."CurrencyId" END) AS currency_id
    FROM entries e LEFT JOIN "Accounts" a ON a."AccountId" = e.account_id
), inventory AS (
    SELECT p.*, c."ShortName" AS currency_code,
           CASE WHEN owner_id IS NULL OR owner_id = 0 OR (kind = 'cash' AND currency_id IS NULL)
                THEN 1 ELSE 0 END AS null_or_zero_keys,
           CASE WHEN kind = 'cash' AND currency_id IS NOT NULL AND
                (c."Id" IS NULL OR (currency_id = 0 AND UPPER(c."ShortName") <> 'PLN'))
                THEN 1 ELSE 0 END AS invalid_cash_currency,
           CASE WHEN linked_id IS NOT NULL AND
                (linked_type <> account_type OR linked_owner = 0 OR linked_owner <> owner_id
                 OR (kind = 'cash' AND linked_currency <> currency_id))
                THEN 1 ELSE 0 END AS account_mismatch,
           CASE WHEN account_id IS NOT NULL AND linked_id IS NULL THEN 1 ELSE 0 END AS orphan_account_link,
           CASE WHEN owner_id <> 0 AND u."Id" IS NULL THEN 1 ELSE 0 END AS missing_user
    FROM projected p
    LEFT JOIN "Currencies" c ON c."Id" = p.currency_id
    LEFT JOIN "Users" u ON u."Id" = p.owner_id
)
SELECT * FROM inventory WHERE null_or_zero_keys + invalid_cash_currency + account_mismatch
                            + orphan_account_link + missing_user <> 0
ORDER BY kind, entry_id;

SELECT "Id", "AccountId" FROM "FinancialAlerts" WHERE "AccountId" IS NOT NULL;
SELECT "Id", "ShortName" FROM "Currencies" WHERE "Id" = 0 OR UPPER("ShortName") = 'PLN';
```

- **Blocking after catch-up:** missing or zero owner or missing cash currency (`null_or_zero_keys`), a cash currency absent from the catalog or referenced ID 0 mapped to a code other than PLN (`invalid_cash_currency`), a link to an existing account with a different kind, owner or cash currency (`account_mismatch`), and an alert with a remaining account scope (`legacy_alert_scope`). Correct the source of the discrepancy and verify the intended owner and currency before editing financial data. An empty database does not need a default-currency row. Historical insight links are allowed.
- **Report only:** a dangling account link with valid owner keys (`orphan_account_link`) and a nonzero owner absent from the user table (`missing_user`). Keep the IDs for reconciliation. Either row can still have a separate blocking flag.
- **Before the expand migration:** use `CAST(NULL AS bigint)` for the cash and bond owner expressions and `CAST(NULL AS int)` for the cash currency expression in the first CTE. The projected keys then come from the historical accounts. Step 2 removes linked trades and dividends whose accounts are missing or have the wrong kind. Review those rows and retain the backup before promotion. Alert links are expected before `MigrateAlertsToCurrencyScope` clears them. To see the precise post-migration inventory before production, rehearse the promotion on a restored copy.

**Operator sequence**

1. Record the deployed build and migration history, run the inventory, and review the planned promotion. Confirm that outstanding cash imports have completed and that all app instances, workers, and other database writers can be stopped.
2. Stop the target Azure Web App (`FinanceManagerApi` or `FinanceManagerApi-dev`), drain all writers, and confirm the database is quiescent.
3. Take a manual logical backup, verify managed recovery availability, and record the restore point. Repeat the inventory against the stopped database. [Database recovery runbook](database-recovery.md)
4. Merge the promotion PR, or approve the protected deployment environment if the job is already waiting there. Keep the old build stopped while CI runs `dotnet ef database update` and deploys the matching artifact.
5. Confirm both CI steps succeeded and verify the applied migration history. Start the deployed build. Outside Development the API refuses to start with pending migrations.
6. Check `/alive`, `/health`, sign-in, Dashboard, Assets, Transactions, rules and alerts. Open a historical account bookmark and confirm it reaches Transactions. With a test owner, create, edit and delete a transaction. Verify the new row's owner and null account link before deletion, and verify the chosen currency for cash. Confirm the legacy account-create request cannot mutate accounts. Repeat the inventory, check for null cash/bond owners and null cash currencies directly, and review application and migration logs before releasing traffic.

**Recovery.** EF commits each migration separately. A failure of a step-12 check rolls back that migration's catch-up and DDL. Earlier migrations in the promotion can already be committed while the pre-epic API is still installed. Keep the app stopped, inspect `__EFMigrationsHistory` and the four counts, then either repair the data and rerun the failed deploy job or restore the verified pre-window backup and matching application build. If migration succeeded but artifact deployment failed, retry the matching artifact deployment with writers still stopped. The step-12 `Down` only restores nullable columns; it cannot undo earlier data migrations, recover previous stamps, or rebuild account links for new owner-only transactions. After accepting new traffic, reconcile post-backup writes before a restore.

## Transaction rule account conditions

`RetireTransactionRuleAccountConditions` is a data-only migration (no schema or snapshot change) that removes every `account` condition from `TransactionRules.ConditionsJson` and sets `IsEnabled = false` on each affected rule. It matches the type case-insensitively after trimming, keeps the other conditions in order, leaves untouched rules and their timestamps alone and changes nothing on a second run. `Down` is a no-op: the removed account ids and the previous enabled flags cannot be rebuilt, so a rollback restores the pre-deploy backup. Exotic padding or key casing that the SQL does not recognise cannot broaden a rule during the dev window between migration and deploy: the runtime builds such a rule disabled (logged once). After the production maintenance window, no rule should still contain an `account` condition: `SELECT COUNT(*) FROM "TransactionRules" WHERE "ConditionsJson" ILIKE '%"account"%'` should be 0.


## Alert currency scope

[MigrateAlertsToCurrencyScope](../../../code/FinanceManager.Api/Migrations/20261123162630_MigrateAlertsToCurrencyScope.cs) adds the nullable `FinancialAlerts.CurrencyId`, copies it only from the alert's own Currency account of the same user, disables all four cash alert types (balance, category, merchant, large transaction) with their trigger state (status, fingerprint, last trigger time and value) reset, and clears `AccountId`. The backfill that reads the new column runs inside `EXEC(N'…')` because SQL Server compiles the generated script as one batch. Subscription alerts stay enabled. It changes data only for rows that still need it, so a second run changes nothing, and it cannot fail on missing or foreign accounts. Reverting drops only the column; the disabled flags and cleared scopes need a backup restore. The contract migration below drops the `AccountId` column.

## Financial accounts contract (#1144)

[DropFinancialAccounts](../../../code/FinanceManager.Api/Migrations/20261125192959_DropFinancialAccounts.cs) is the irreversible end of epic #1126. In this order it drops the dividend foreign key to `Accounts`, the seven account-first indexes and `IX_Accounts_AccountType_UserId`, the `AccountId` column of `CurrencyEntries`, `BondEntries`, `InvestmentTransactions`, `DividendPayments`, `FinancialAlerts` and `FinancialInsights`, the stored running `Value` of `CurrencyEntries`, `BondEntries` and `InvestmentTransactions`, and finally the `Accounts` table. On SQL Server, EF drops a column's default constraint before the column (`InvestmentTransactions.Value` has one from `StoreInvestmentRunningUnits`). PostgreSQL drops the `Accounts` identity sequence with the table. Owner keys, owner indexes and the label join tables, with their pinned `CurrencyAccountEntryEntryId` and `BondAccountEntryEntryId` columns, stay. No row is deleted or rewritten. Only the account names and links are lost, and nothing archives them. `Down` throws `NotSupportedException("Restore from the pre-contract backup.")`.

It can ship in the same maintenance window as the owner-key cutover (#1138), as long as that cutover's inventory came back clean. `dotnet ef database update` applies `EnforceTransactionOwnerKeys` first, and that migration's precondition aborts the promotion before this one runs. The build that is still running maps `AccountId` and `Value`, so no instance of it may run once this migration applies.

1. **Pre-check.** On the database to be migrated, confirm that `EnforceTransactionOwnerKeys` is applied, or is pending in this same promotion with a clean inventory. Record `SELECT COUNT(*) FROM "Accounts"` in the release notes. Record the row count of each of `CurrencyEntries`, `BondEntries`, `InvestmentTransactions`, `DividendPayments`, `FinancialAlerts`, `FinancialInsights`, `FinancialLabels`, `CurrencyAccountEntryFinancialLabel` and `InvestmentTransactionFinancialLabel`. Confirm that no owner key is missing: `SELECT COUNT(*) FROM "CurrencyEntries" WHERE "UserId" IS NULL OR "CurrencyId" IS NULL` and `SELECT COUNT(*) FROM "BondEntries" WHERE "UserId" IS NULL` both return 0.
2. **Stop writers.** Stop the target Azure Web App (`FinanceManagerApi` or `FinanceManagerApi-dev`), drain all writers, and confirm the database is quiescent. Repeat the row counts against the stopped database.
3. **Backup.** Take the manual logical backup from the [database recovery runbook](database-recovery.md#3-manual-backup-procedure) and verify that it is readable. It is the only rollback.
4. **Deploy.** Merge the promotion, or approve the waiting protected deployment environment. Keep the old build stopped while CI runs `dotnet ef database update` and deploys the matching API build, then start the app.
5. **Post-check.** `Accounts` and every dropped column are gone (`INFORMATION_SCHEMA.TABLES` and `INFORMATION_SCHEMA.COLUMNS`). Each recorded row count is unchanged. A guest and a real user load Dashboard and Transactions.
6. **Rollback.** Stop the app, restore the backup and redeploy the application build that matches it ([restore from backup](database-recovery.md#6-restore-from-backup)). `dotnet ef database update` to an earlier migration fails by design. Writes made after the backup must be reconciled by hand.
