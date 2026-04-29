# Architecture

## Core Sections (Required)

### 1) Architectural Style

- Primary style: layered modular monolith with feature-grouped UI and controller folders
- Why this classification: the solution is split into dedicated projects for UI host, API, application services, domain contracts, and infrastructure implementations, but they are deployed together as one web/API system sharing one `AppDbContext`
- Primary constraints:
  - Browser UI is a Blazor WebAssembly client that talks to the API over HTTP/JSON and SignalR
  - Persistence is mediated through EF Core repositories backed by either SQL Server or PostgreSQL
  - External stock, currency, and AI integrations are optional/runtime-configured and wrapped behind provider abstractions

### 2) System Flow

```text
Razor component -> typed HttpClient -> API controller -> application/domain service -> repository or external provider -> JSON response or persisted update
```

Representative flow for stock prices:

1. A Razor page or component uses `StockPriceHttpClient` from `FinanceManager.Components\HttpClients`.
2. `StockPriceHttpClient` calls `api/StockPrice/...` endpoints on the backend.
3. `StockPriceController` validates request inputs and coordinates repositories/services.
4. `IStockPriceProvider` / `IStockMarketService` in the application layer decide whether to use stored data or fetch from external providers.
5. `StockPriceRepository` reads/writes stock prices through `AppDbContext`, while `AlphaVantageClient` and currency services call external APIs when local data is incomplete.
6. The API returns DTO/entity-shaped JSON to the client, or broadcasts progress over SignalR for import flows.

### 3) Layer/Module Responsibilities

| Layer or module | Owns | Must not own | Evidence |
|-----------------|------|--------------|----------|
| `FinanceManager` | Browser bootstrap, root component registration, base `HttpClient` setup | Controller logic, EF Core wiring | `code\FinanceManager\Program.cs` |
| `FinanceManager.Components` | UI composition, typed HTTP clients, local/session state, client-side caches | Direct DB access, web-host middleware | `code\FinanceManager.Components\ServiceCollectionExtension.cs`, `code\FinanceManager.Components\HttpClients\StockPriceHttpClient.cs` |
| `FinanceManager.Api` | Route definitions, auth/CORS, background services, SignalR hub mapping | Domain persistence details, browser-only storage logic | `code\FinanceManager.Api\Program.cs`, `code\FinanceManager.Api\Controllers\StockPriceController.cs` |
| `FinanceManager.Application` | Use-case orchestration, pricing/insight services, provider coordination, seeding | Transport-specific concerns, `DbContext` access | `code\FinanceManager.Application\ServiceCollectionExtension.cs`, `code\FinanceManager.Application\Providers\StockPriceProvider.cs` |
| `FinanceManager.Domain` | Entities, repository interfaces, service interfaces, commands/value objects | ASP.NET or EF Core implementations | `code\FinanceManager.Domain\Services\IStockPriceProvider.cs`, `code\FinanceManager.Domain\Repositories` |
| `FinanceManager.Infrastructure` | EF Core model, repository implementations, external API and AI adapters | HTTP endpoint definitions, page rendering | `code\FinanceManager.Infrastructure\Contexts\AppDbContext.cs`, `code\FinanceManager.Infrastructure\Repositories\StockPriceRepository.cs`, `code\FinanceManager.Infrastructure\Services\Ai\ServiceCollectionExtension.cs` |
| `AppHost` + `ServiceDefaults` | Local orchestration, service discovery, OpenTelemetry, resilience defaults | Feature logic | `code\AppHost\AppHost.cs`, `code\ServiceDefaults\Extensions.cs` |

### 4) Reused Patterns

| Pattern | Where found | Why it exists |
|---------|-------------|---------------|
| Dependency-injection registration extensions | `code\FinanceManager.Application\ServiceCollectionExtension.cs`, `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs`, `code\FinanceManager.Components\ServiceCollectionExtension.cs` | Keeps startup wiring grouped by layer/project |
| Repository pattern | `code\FinanceManager.Domain\Repositories`, `code\FinanceManager.Infrastructure\Repositories\StockPriceRepository.cs` | Separates domain contracts from EF Core persistence |
| Typed HTTP client adapters | `code\FinanceManager.Components\HttpClients\*.cs` | Keeps API route details out of Razor pages/components |
| Hosted background services + channel abstractions | `code\FinanceManager.Api\Program.cs` | Runs asynchronous insights, label setting, and import jobs outside request handlers |
| Provider fallback strategy | `code\FinanceManager.Application\Providers\StockPriceProvider.cs`, `code\FinanceManager.Infrastructure\Services\Ai\ServiceCollectionExtension.cs`, `code\FinanceManager.Api\appsettings.json` | Falls back from cached/local data to external stock or AI providers |
| Razor code-behind | `code\FinanceManager.Components\Components\AssetsPage.razor` + `.razor.cs`, `code\FinanceManager.Components\Components\FinancialAccounts\...` | Splits markup from complex component logic |

### 5) Known Architectural Risks

- The repository mixes source code under `code\` with published static assets at the repo root, which increases the chance of documenting or editing generated output instead of source.
- Runtime/database intent is split across `AppHost` PostgreSQL orchestration and development config that still points at SQL Server, so environment drift is possible.
- A few client and controller paths rely on broad exception handling or return-value fallbacks, which can obscure operational failures.

### 6) Evidence

- `code\FinanceManager.Api\Program.cs`
- `code\FinanceManager\Program.cs`
- `code\FinanceManager.Components\HttpClients\StockPriceHttpClient.cs`
- `code\FinanceManager.Api\Controllers\StockPriceController.cs`
- `code\FinanceManager.Application\Providers\StockPriceProvider.cs`
- `code\FinanceManager.Infrastructure\Repositories\StockPriceRepository.cs`
- `code\ServiceDefaults\Extensions.cs`
