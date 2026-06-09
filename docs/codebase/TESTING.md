# Testing Patterns

## Core Sections (Required)

### 1) Test Stack and Commands

- Primary test framework: xUnit v3 on Microsoft.Testing.Platform
- Assertion/mocking tools: xUnit assertions, Moq, `WebApplicationFactory`, EF Core InMemory provider, coverlet
- Commands:

```bash
dotnet test .\code\FinanceManager.slnx
dotnet test .\code\FinanceManager.Tests.Unit\FinanceManager.Tests.Unit.csproj
dotnet test .\code\FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj
dotnet test .\code\FinanceManager.Tests.Unit\FinanceManager.Tests.Unit.csproj --collect:"XPlat Code Coverage"
```

### 2) Test Layout

- Test file placement pattern: dedicated test projects instead of co-located tests
- Naming convention: `*Tests.cs` classes grouped by layer/feature (for example `Api\Controllers\StockPriceControllerTests.cs`)
- Setup files and where they run: `code\FinanceManager.Tests.Integration\FinanceManagerApiTestApp.cs`, `code\FinanceManager.Tests.Integration\Controllers\ControllerTests.cs`, `code\FinanceManager.Tests.Integration\TestDatabase.cs`, `code\FinanceManager.Tests.Integration\OptionsProvider.cs`

### 3) Test Scope Matrix

| Scope | Covered? | Typical target | Notes |
|-------|----------|----------------|-------|
| Unit | yes | API controllers, application services, domain entities/extensions | Uses `Moq` and direct controller/service construction |
| Integration | yes | API endpoints and repository-backed behavior through `WebApplicationFactory` | Uses in-memory EF Core and service overrides |
| E2E | no | `[TODO]` | `.playwright-mcp` logs exist at repo root, but no browser-test project/config was found under `code\` |

### 4) Mocking and Isolation Strategy

- Main mocking approach: `Moq` for unit tests; DI overrides plus in-memory EF Core for integration tests
- Isolation guarantees: integration tests create a fresh `TestDatabase`, remove startup hosted services that would touch the DB, and clear auth headers between tests
- Common failure mode in tests: hosted services can interfere with startup, which is why integration tests explicitly remove `DatabaseInitializer` and `LabelSetterStartupService`

### 5) Coverage and Quality Signals

- Coverage tool + threshold: coverlet collector / MSBuild; threshold `[TODO]` not found
- Current reported coverage: `[TODO]` no consolidated coverage report was found in the checked-in repo
- Known gaps/flaky areas: no evidence of browser-level E2E coverage; integration tests rely on EF Core InMemory rather than exercising a relational engine

### 6) Evidence

- `code\global.json`
- `code\FinanceManager.Tests.Unit\FinanceManager.Tests.Unit.csproj`
- `code\FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj`
- `code\FinanceManager.Tests.Unit\Api\Controllers\StockPriceControllerTests.cs`
- `code\FinanceManager.Tests.Integration\Controllers\ControllerTests.cs`
- `code\FinanceManager.Tests.Integration\FinanceManagerApiTestApp.cs`
- `.github\workflows\ci.yml`

