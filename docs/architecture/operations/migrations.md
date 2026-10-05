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
