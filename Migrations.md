```
dotnet tool install dotnet-ef -g
dotnet tool update --global dotnet-ef

dotnet ef migrations add init -s code\FinanceManager.Api\FinanceManager.Api.csproj

dotnet ef database update -s code\FinanceManager.Api\FinanceManager.Api.csproj


dotnet ef migrations add stockPriceUpdate_PrecisionUpdate -s code\FinanceManager.Api\FinanceManager.Api.csproj
```