# Production Database Backup and Rollback Runbook

[Architecture index](../README.md) · [Owning arc42 section](../07-deployment-view.md)

Commands in this guide run from the repository root unless an explicit `cd` is shown.

This runbook documents the manual database safety procedure for FinanceManager production releases.

Scope:
- This runbook targets a Supabase PostgreSQL production database. Confirm the actual host/project and provider before use; repository configuration alone does not verify live hosting.
- This document covers manual backup, restore, and EF Core migration rollback.
- This document does not introduce any automatic backup step in CI/CD.

## 1. Preconditions

Before applying a production release or a manual migration:

1. Confirm the exact commit being deployed.
2. Confirm the last applied EF migration in production.
3. Export the production connection string into a temporary shell variable.
4. Create a fresh logical backup and store it outside the repo.

PowerShell example:

```powershell
$env:SUPABASE_DB_URL="postgresql://postgres.[PROJECT-REF]:[PASSWORD]@aws-0-[REGION].pooler.supabase.com:5432/postgres"
New-Item -ItemType Directory -Force -Path .\backups | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
```

Connection string note:
- Use the Supabase `Connect` value for the production project.
- Supabase documents the session pooler connection string as the default choice for CLI backup/restore work.
- If your environment supports IPv6 or you have the Supabase IPv4 add-on, you can use the direct connection string instead.

## 2. Inspect Current Migration State

List migrations known to the application:

```powershell
dotnet ef migrations list `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj
```

Check which migration is currently applied in Supabase:

```sql
select "MigrationId", "ProductVersion"
from "__EFMigrationsHistory"
order by "MigrationId";
```

Run that query in the Supabase SQL Editor before the release. The last row is the current production migration.

## 3. Manual Backup Procedure

Supabase offers two backup paths. Use both when possible:

1. Supabase managed backups / PITR if the project plan supports them.
2. A manual logical backup taken immediately before deployment.

### Option A: Supabase managed backups

In the Supabase Dashboard:

1. Open the project.
2. Go to `Database` -> `Backups`.
3. Confirm that recent backups exist.
4. If PITR is enabled, confirm the retention window and latest recoverable timestamp.

Notes:
- Supabase daily backups are available on paid plans.
- PITR gives finer restore granularity than daily backups.
- Restoring a Supabase backup causes downtime while the project is restored.
- Supabase backups do not restore deleted Storage objects, only their database metadata.

### Option B: Manual logical backup before deployment

Supabase recommends logical exports with the CLI `db dump` command for manual backups. For this repository, keep a schema backup and a data backup.

Schema backup:

```powershell
supabase db dump --db-url "$env:SUPABASE_DB_URL" `
  -f ".\backups\fm-schema-$timestamp.sql"
```

Data backup:

```powershell
supabase db dump --db-url "$env:SUPABASE_DB_URL" `
  --data-only `
  --use-copy `
  -x "storage.buckets_vectors" `
  -x "storage.vector_indexes" `
  -f ".\backups\fm-data-$timestamp.sql"
```

If you use custom database roles and need them recreated separately:

```powershell
supabase db dump --db-url "$env:SUPABASE_DB_URL" `
  --role-only `
  -f ".\backups\fm-roles-$timestamp.sql"
```

After the files are generated:

1. Verify they are non-empty.
2. Move them to encrypted off-repo storage.
3. Record the deployed commit, timestamp, and current migration in the release notes or deployment log.

## 4. Deploy / Apply Migrations

This application applies pending EF Core migrations on startup through `DatabaseInitializer`, which calls `MigrateAsync()` for relational databases.

If you need to apply the migrations manually first, run:

```powershell
dotnet ef database update `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --connection "$env:SUPABASE_DB_URL"
```

## 5. Roll Back to the Previous EF Migration

Use this path when:
- The latest migration is wrong.
- The failure is schema-related.
- Reverting the schema is enough and no important post-deploy data must be preserved.

Do not use this as the primary recovery path after data corruption or destructive data backfills. In those cases, restore from backup instead.

### Step 1: Identify the target migration and compatible application artifact

Stop/drain all application instances and prevent the failed release from restarting or being redeployed while recovery is in progress. `DatabaseInitializer` automatically applies pending migrations on startup, so leaving the failed artifact running can immediately reapply the migration being reverted. Take the required backup and select a previously validated application artifact compatible with the rollback target.

Use a recovery checkout/artifact that contains the bad migration and its reviewed `Down()` method for the EF rollback command; the application artifact started afterward must exclude that bad migration. These are distinct requirements.

Find the migration immediately before the bad one:

```powershell
dotnet ef migrations list `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj
```

Example:

```text
20260604114844_AddPasswordResetTokens
20260605232611_AddExternalServiceConfigurations
```

If `20260605232611_AddExternalServiceConfigurations` is the bad release, the rollback target is `20260604114844_AddPasswordResetTokens`.

### Step 2: Move the database back to that migration

```powershell
dotnet ef database update 20260604114844_AddPasswordResetTokens `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --connection "$env:SUPABASE_DB_URL"
```

Important:
- EF Core executes the `Down()` methods between the current migration and the target migration.
- This can drop columns, tables, indexes, or constraints.
- If the reverted migration changed data, the rollback may still lose data. Take a backup first.

### Step 3: Verify rollback

1. Query `__EFMigrationsHistory` again and confirm the bad migration is gone.
2. Deploy/start only the compatible pre-migration application artifact selected in Step 1. Keep the failed/latest artifact stopped; its startup migration initializer would reapply the reverted migration.
3. Query `__EFMigrationsHistory` again after startup to confirm the bad migration remains absent, then verify login, dashboard load, and core account flows.
4. Review application logs for migration or startup failures before restoring traffic.

## 6. Restore From Backup

Stop/drain application writers and prevent automatic restarts before restoring. Select an application artifact compatible with the backup schema and data; after restoration, start that artifact and verify migration history before restoring traffic. Starting an artifact containing later migrations can change the restored schema immediately.

Use this path when:
- Data was corrupted.
- A migration or release damaged data contents, not just schema shape.
- `dotnet ef database update <PreviousMigration>` is not sufficient.

Choose one of the following restore paths.

### Path A: Restore from Supabase managed backup or PITR

Preferred for production incidents when available.

1. Open Supabase Dashboard.
2. Go to `Database` -> `Backups`.
3. Choose the latest safe backup before the failed deployment.
4. If PITR is enabled, choose the exact timestamp immediately before the incident.
5. Confirm the restore.
6. Wait for Supabase to complete the restore and bring the project back online.
7. Re-run smoke tests against the restored project.

Notes:
- The database is unavailable during restore.
- If the project uses custom roles, reset their passwords after restore.
- If the project uses replication slots or subscriptions outside the default Supabase setup, they must be dropped before restore and recreated afterwards.

### Path B: Restore the manual logical backup

Use this when you need to restore into a fresh environment, or when managed backups are unavailable.

Recommended target:
- A new Supabase project for validation, or
- An empty PostgreSQL database, not an in-place restore over a live production database.

Restore schema:

```powershell
psql `
  --single-transaction `
  --variable ON_ERROR_STOP=1 `
  --file ".\backups\fm-schema-YYYYMMDD-HHMMSS.sql" `
  --dbname "$env:SUPABASE_DB_URL"
```

Restore data:

```powershell
psql `
  --single-transaction `
  --variable ON_ERROR_STOP=1 `
  --command "SET session_replication_role = replica" `
  --file ".\backups\fm-data-YYYYMMDD-HHMMSS.sql" `
  --dbname "$env:SUPABASE_DB_URL"
```

If roles were exported and need restoring:

```powershell
psql `
  --single-transaction `
  --variable ON_ERROR_STOP=1 `
  --file ".\backups\fm-roles-YYYYMMDD-HHMMSS.sql" `
  --dbname "$env:SUPABASE_DB_URL"
```

Recommended validation after restore:

1. Confirm expected row counts for critical tables.
2. Confirm `__EFMigrationsHistory` matches the backup point.
3. Sign in and validate account listing, balances, and transaction history.

## 7. Decision Guide

Use EF migration rollback when:
- The problem is limited to the newest migration.
- The rollback can be expressed safely by existing `Down()` methods.
- You do not need to recover overwritten or deleted data.

Use Supabase backup restore when:
- Data integrity is in doubt.
- A bad release wrote incorrect values.
- A migration performed destructive or irreversible data changes.
- Multiple migrations or app writes happened after the failure.

## 8. Repo-Specific Commands

Common commands for this repository:

```powershell
dotnet tool update --global dotnet-ef

dotnet ef migrations list `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj

dotnet ef database update `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --connection "$env:SUPABASE_DB_URL"
```

## 9. References

This runbook was verified against:

- Supabase Database Backups docs: https://supabase.com/docs/guides/platform/backups
- Supabase Backup and Restore using the CLI docs: https://supabase.com/docs/guides/platform/migrating-within-supabase/backup-restore
- EF Core migration application docs: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- EF Core CLI docs: https://learn.microsoft.com/en-us/ef/core/cli/dotnet
