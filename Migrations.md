## EF Core migrations

```powershell
dotnet tool install dotnet-ef -g
dotnet tool update --global dotnet-ef

dotnet ef migrations add init `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj

dotnet ef database update `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj

dotnet ef migrations add stockPriceUpdate_PrecisionUpdate `
  --project code/FinanceManager.Api/FinanceManager.Api.csproj `
  --startup-project code/FinanceManager.Api/FinanceManager.Api.csproj
```

## Production rollback and backup

Production rollback is documented in [RUNBOOK.md](RUNBOOK.md).

Use that runbook for:
- manual Supabase backups before deployment
- restoring from Supabase backups or manual logical dumps
- reverting production to a previous EF Core migration
