```
dotnet tool install dotnet-ef -g

dotnet ef migrations add init -s code\FinanceManager.Api\FinanceManager.Api.csproj

dotnet ef database update -s code\FinanceManager.Api\FinanceManager.Api.csproj


dotnet ef migrations add stockPriceUpdate_PrecisionUpdate -s code\FinanceManager.Api\FinanceManager.Api.csproj
```