# Coding Conventions

## Core Sections (Required)

### 1) Naming Rules

| Item | Rule | Example | Evidence |
|------|------|---------|----------|
| Files | PascalCase for C# and Razor files; tests end with `Tests.cs`; code-behind pairs use `.razor` + `.razor.cs` | `StockPriceController.cs`, `AssetsPage.razor`, `StockPriceControllerTests.cs` | `code\FinanceManager.Api\Controllers\StockPriceController.cs`, `code\FinanceManager.Components\Components\AssetsPage.razor`, `code\FinanceManager.UnitTests\Api\Controllers\StockPriceControllerTests.cs` |
| Functions/methods | PascalCase methods | `AddApplicationApi`, `GetStockPrice`, `GetPricePerUnitAsync` | `code\FinanceManager.Application\ServiceCollectionExtension.cs`, `code\FinanceManager.Api\Controllers\StockPriceController.cs`, `code\FinanceManager.Application\Providers\StockPriceProvider.cs` |
| Types/interfaces | PascalCase types; interfaces prefixed with `I` | `StockPriceRepository`, `IStockPriceProvider` | `code\.editorconfig`, `code\FinanceManager.Infrastructure\Repositories\StockPriceRepository.cs`, `code\FinanceManager.Domain\Services\IStockPriceProvider.cs` |
| Constants/env vars | Environment variables use uppercase snake case; private/internal fields use `_camelCase` | `FINANCE_MANAGER_DB_KEY`, `_currencyExchangeMock` | `code\FinanceManager.Api\appsettings.Development.json`, `code\.editorconfig`, `code\FinanceManager.IntegrationTests\Controllers\StockPriceControllerTests.cs` |

### 2) Formatting and Linting

- Formatter: `dotnet format` using `code\.editorconfig`
- Linter: Roslyn/.NET build-time code-style analysis enforced by `code\Directory.Build.props`
- Most relevant enforced rules: treat warnings as errors, nullable enabled, namespaces should match folders, interfaces begin with `I`, private/internal fields should start with `_`
- Run commands: `dotnet format .\code --verify-no-changes --verbosity diagnostic`, `dotnet build .\code\FinanceManager.slnx`

### 3) Import and Module Conventions

- Import grouping/order: `.editorconfig` disables separate import groups and does not force `System` usings first
- Alias vs relative import policy: no custom import alias scheme detected; project boundaries are expressed through `ProjectReference` and namespaces
- Public exports/barrel policy: none detected; classes are referenced directly by namespace/type rather than barrel exports

### 4) Error and Logging Conventions

- Error strategy by layer: API controllers usually validate inputs and return `BadRequest` / `NotFound`; infrastructure adapters commonly log and return `null`, `[]`, or empty chat responses; some client-side services catch `Exception` and return booleans/defaults
- Logging style and required context fields: `ILogger<T>` with structured messages is common in API/infrastructure (`logger.LogWarning("...", value)` / `logger.LogError(ex, "...", value)`)
- Sensitive-data redaction rules: `[TODO]` no explicit repo-wide redaction policy or sanitizer was found

### 5) Testing Conventions

- Test file naming/location rule: tests live in dedicated `FinanceManager.UnitTests` and `FinanceManager.IntegrationTests` projects and use `*Tests.cs`
- Mocking strategy norm: unit tests use `Moq`; integration tests override DI registrations and use in-memory EF Core plus generated JWTs
- Coverage expectation: coverlet is enabled, but no explicit minimum coverage threshold was found

### 6) Evidence

- `code\.editorconfig`
- `code\Directory.Build.props`
- `.github\workflows\ci.yml`
- `code\FinanceManager.Api\Controllers\StockPriceController.cs`
- `code\FinanceManager.IntegrationTests\Controllers\StockPriceControllerTests.cs`
- `code\FinanceManager.Components\Services\LoginService.cs`

