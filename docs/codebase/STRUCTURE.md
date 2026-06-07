# Codebase Structure

## Core Sections (Required)

### 1) Top-Level Map

| Path | Purpose | Evidence |
|------|---------|----------|
| `code\` | Source-of-truth .NET solution and all runtime projects | repo root directory listing, `code\` directory listing |
| `code\FinanceManager\` | Blazor WebAssembly client bootstrap project | `code\FinanceManager\Program.cs`, `code\FinanceManager\FinanceManager.WebUi.csproj` |
| `code\FinanceManager.Components\` | Shared Razor UI, typed HTTP clients, client-side services | `code\FinanceManager.Components\Components`, `code\FinanceManager.Components\HttpClients`, `code\FinanceManager.Components\ServiceCollectionExtension.cs` |
| `code\FinanceManager.Api\` | ASP.NET Core API, static-file host, SignalR hub, migrations, background services | `code\FinanceManager.Api\Program.cs`, `code\FinanceManager.Api\Controllers`, `code\FinanceManager.Api\Hubs` |
| `code\FinanceManager.Application\` | Application services, providers, options, seeding orchestration | `code\FinanceManager.Application\ServiceCollectionExtension.cs`, `code\FinanceManager.Application\Services` |
| `code\FinanceManager.Domain\` | Domain entities, repositories, commands, value objects, service contracts | `code\FinanceManager.Domain\Entities`, `code\FinanceManager.Domain\Repositories`, `code\FinanceManager.Domain\Services` |
| `code\FinanceManager.Infrastructure\` | EF Core `DbContext`, repositories, external API adapters, AI providers | `code\FinanceManager.Infrastructure\Contexts\AppDbContext.cs`, `code\FinanceManager.Infrastructure\Repositories`, `code\FinanceManager.Infrastructure\Services` |
| `code\AppHost\` | Aspire local orchestration for API + PostgreSQL | `code\AppHost\AppHost.cs` |
| `code\ServiceDefaults\` | Shared telemetry, service-discovery, resilience, health defaults | `code\ServiceDefaults\Extensions.cs` |
| `code\FinanceManager.Tests.Unit\` | Unit tests against controllers, application services, domain types | `code\FinanceManager.Tests.Unit` directory listing, `code\FinanceManager.Tests.Unit\FinanceManager.Tests.Unit.csproj` |
| `code\FinanceManager.Tests.Integration\` | Integration tests using `WebApplicationFactory` + in-memory EF Core | `code\FinanceManager.Tests.Integration` directory listing, `code\FinanceManager.Tests.Integration\FinanceManagerApiTestApp.cs` |
| `.github\` | CI/CD and repo-specific Copilot instructions | `.github\workflows\ci.yml`, `.github\copilot-instructions.md` |
| `resources\` / `sample-data\` | Non-code data inputs used by the app and imports | repo root directory listing, `code\FinanceManager.Api\appsettings.Development.json` |
| repo root published files (`index.html`, `_framework\`, `_content\`, compressed assets) | Built static site output checked into the repository root | repo root directory listing |

### 2) Entry Points

- Main runtime entry: `code\FinanceManager.Api\Program.cs`
- Secondary entry points (worker/cli/jobs): `code\FinanceManager\Program.cs` (WASM client bootstrap), `code\AppHost\AppHost.cs` (Aspire app host), hosted services registered in `code\FinanceManager.Api\Program.cs`
- How entry is selected (script/config): CI builds `./code`, deploys `code\FinanceManager.Api\FinanceManager.Api.csproj`, and the API host serves the static client via `UseBlazorFrameworkFiles`, `UseStaticFiles`, and `MapFallbackToFile("index.html")`

### 3) Module Boundaries

| Boundary | What belongs here | What must not be here |
|----------|-------------------|------------------------|
| `FinanceManager` | WASM host wiring, root component registration, browser bootstrapping | API controllers, EF Core persistence |
| `FinanceManager.Components` | Pages/components, typed HTTP clients, browser-local services/caches | Direct database access, ASP.NET host setup |
| `FinanceManager.Api` | HTTP endpoints, auth/CORS/SignalR wiring, background-service registration | Browser-only storage concerns, reusable domain calculations |
| `FinanceManager.Application` | Use-case orchestration, pricing/insight services, seeding, provider composition | ASP.NET endpoint definitions, EF Core `DbContext` details |
| `FinanceManager.Domain` | Entities, contracts, commands, value objects, domain abstractions | HTTP transport code, concrete infrastructure adapters |
| `FinanceManager.Infrastructure` | Repository implementations, `AppDbContext`, external APIs, AI clients | Route handling, page rendering |
| `FinanceManager.Tests.Unit` / `FinanceManager.Tests.Integration` | Verification only | Production runtime wiring |

### 4) Naming and Organization Rules

- File naming pattern: PascalCase for `.cs` and `.razor` files (examples: `StockPriceController.cs`, `AssetsPage.razor`, `ImportCurrencyEntriesComponent.razor.cs`)
- Directory organization pattern: primarily layer/project-based (`Api`, `Application`, `Domain`, `Infrastructure`) with feature folders inside projects (`Controllers\Accounts`, `Components\FinancialAccounts`)
- Import aliasing or path conventions: no custom import aliases detected; namespace/folder alignment is encouraged by `.editorconfig` (`dotnet_style_namespace_match_folder = true`)

### 5) Evidence

- repo root directory listing
- `code\` directory listing
- `code\FinanceManager.Api\Program.cs`
- `code\FinanceManager\Program.cs`
- `code\AppHost\AppHost.cs`
- `code\.editorconfig`

