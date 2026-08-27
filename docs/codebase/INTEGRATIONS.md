# External Integrations

## Core Sections (Required)

### 1) Integration Inventory

| System | Type (API/DB/Queue/etc) | Purpose | Auth model | Criticality | Evidence |
|--------|---------------------------|---------|------------|-------------|----------|
| SQL Server | Relational database | Primary persistence option for accounts, users, prices, labels, imports | Connection string from config/env | High | `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs`, `code\FinanceManager.Api\appsettings.Development.json` |
| PostgreSQL | Relational database | Alternate persistence option and Aspire-local database | Connection string / service binding | High | `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs`, `code\AppHost\AppHost.cs` |
| Alpha Vantage | External HTTP API | Stock symbol search, daily price history, listings | API key | High | `code\FinanceManager.Infrastructure\Services\Stocks\AlphaVantageClient.cs`, `code\FinanceManager.Api\appsettings.json` |
| Fawaz Ahmed Currency API via jsDelivr | External HTTP API | Currency exchange-rate lookup | No API key | Medium | `code\FinanceManager.Infrastructure\Features\FinancialAccounts\Currencies\Providers\FawazAhmedCurrencyApiClient.cs` |
| OpenRouter | External AI API | AI chat provider for insights/label generation | API key | Medium | `code\FinanceManager.Infrastructure\Services\Ai\OpenRouterChatClient.cs`, `code\FinanceManager.Api\appsettings.json` |
| GitHub Models via Copilot SDK | External AI service | Alternate AI provider in configured fallback chain | Copilot SDK session auth `[TODO]` | Medium | `code\FinanceManager.Infrastructure\Services\Ai\CopilotChatClient.cs`, `code\FinanceManager.Api\appsettings.json` |
| Ollama | Local/remote AI endpoint | Final AI fallback provider | No auth detected in code | Medium | `code\FinanceManager.Infrastructure\Services\Ai\OllamaChatClient.cs`, `code\FinanceManager.Api\appsettings.json` |
| SignalR hub (`/hubs/currency-import`) | Realtime transport | Import job progress/events between API and browser | JWT via query-string token handling | Medium | `code\FinanceManager.Api\Program.cs` |
| MCP/OAuth (`/mcp`, `/connect/*`) | Stateless MCP transport and OAuth authorization server | Gives configured AI clients owner-isolated read access to Finance Manager data | OpenIddict authorization code, refresh token, resource, and `mcp` scope validation | High | `code\FinanceManager.Api\Mcp`, `code\FinanceManager.Api\Controllers\McpOAuthController.cs`, `docs\deployment.md` |
| Maintenance API (`/api/PriceBackfill`, `/api/maintenance/logs`) | Dedicated maintenance & diagnostics HTTP API | Operational triggering (price backfill) and read-only runtime log inspection for incident diagnostics | API key via `X-Maintenance-Key` request header (hashed in database / configuration fallback) | Medium | `code\FinanceManager.Api\Features\Maintenance\Controllers\PriceBackfillController.cs`, `code\FinanceManager.Application\Shared\Maintenance\*`, `.claude\skills\fm-maintenance\SKILL.md` |
| Browser local/session storage | Browser-side storage | Persist user session and login state | Same-origin browser storage | Medium | `code\FinanceManager\Program.cs`, `code\FinanceManager.Components\Services\LoginService.cs` |

### 2) Data Stores

| Store | Role | Access layer | Key risk | Evidence |
|-------|------|--------------|----------|----------|
| EF Core relational database (`AppDbContext`) | System-of-record for users, accounts, entries, prices, labels, imports | `FinanceManager.Infrastructure\Repositories\*` via `AppDbContext` | Database-provider drift between SQL Server and PostgreSQL setups | `code\FinanceManager.Infrastructure\Contexts\AppDbContext.cs`, `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs` |
| In-memory EF Core database | Test-only persistence for integration tests | `FinanceManager.Tests.Integration\TestDatabase.cs` | Divergence from relational behavior in production | `code\FinanceManager.Tests.Integration\TestDatabase.cs`, `code\FinanceManager.Tests.Integration\FinanceManagerApiTestApp.cs` |
| OpenIddict entities in `AppDbContext` | MCP OAuth clients, authorizations, scopes, and refresh/token state | OpenIddict EF Core stores and startup reconciliation | Grant revocation and persistent encryption keys must remain operational across deployments | `code\FinanceManager.Infrastructure\Contexts\AppDbContext.cs`, `code\FinanceManager.Infrastructure\OAuth`, `docs\deployment.md` |
| `IMemoryCache` | Client caches and stock-price memoization | `FinanceManager.Components` and `FinanceManager.Application\Providers` | Cache scope is per process/browser and not shared across instances | `code\FinanceManager.Components\ServiceCollectionExtension.cs`, `code\FinanceManager.Application\Providers\StockPriceProvider.cs` |
| Browser local/session storage | User session persistence in the frontend | `LoginService` | Token/session handling depends on browser storage state | `code\FinanceManager.Components\Services\LoginService.cs` |

### 3) Secrets and Credentials Handling

- Credential sources: appsettings files, environment variables (notably `FINANCE_MANAGER_DB_KEY` and optional OTLP endpoint), ASP.NET User Secrets (`UserSecretsId`), and runtime options sections
- Hardcoding checks: development/test JWT signing keys and a development SQL Server connection string are committed in `appsettings.*`; external AI/stock API keys are present as config slots but blank in the checked-in config
- Maintenance API key (`X-Maintenance-Key`): Database-managed keys are stored as hashes; an optional configuration fallback can also be supplied. Maintenance endpoints enforce header-only validation, never accept keys in query strings, never echo or return keys in responses, and keys must never be logged.
- Rotation or lifecycle notes: MCP OAuth signing/encryption certificate storage and disruptive rotation are documented in `docs\deployment.md`; a general rotation runbook for the other application secrets is still `[TODO]`

### 4) Reliability and Failure Behavior

- Retry/backoff behavior: shared HTTP clients get the standard .NET resilience handler via `ServiceDefaults`, but individual external adapters often just log and return empty/null results
- Timeout policy: `HttpClientResilience` timeouts are configurable in appsettings and applied in `ServiceDefaults`; OpenRouter timeout is separately configurable in `OpenRouterOptions`
- Circuit-breaker or fallback behavior: HTTP circuit-breaker sampling is configured in `ServiceDefaults`; AI calls are wired through a fallback chain in config and DI; stock price reads first check repository/cache before hitting Alpha Vantage
- Fawaz currency data is free, requires no API key, and has no provider request-rate limit. It is published as daily date-versioned npm packages beginning on `2024-03-02`; earlier dates return `OutOfRange` without an HTTP request so another configured provider can resolve them.
- Maintenance endpoints (`/api/PriceBackfill`, `/api/maintenance/logs`): Protected by rate limiting, returning `429 Too Many Requests` on violation. When unconfigured, endpoints return `404 Not Found` to mask their existence. Read-only log queries use bounded `skip`/`take` pagination (`take` max 200), UTC range/level/text filters, and newest-first ordering.

### 5) Observability for Integrations

- Logging around external calls: yes, in stock, currency, and AI client wrappers
- Metrics/tracing coverage: yes, service defaults enable OpenTelemetry logging, metrics, tracing, and optional OTLP export
- Dedicated read-only maintenance log inspection: `GET /api/maintenance/logs` provides authorized maintenance workers with structured, bounded query access to runtime logs. It accepts `skip`, `take`, `fromUtc`, `toUtc`, `level`, and `search`; responses contain `items` and `totalCount`, and never contain maintenance keys.
- Missing visibility gaps: client-side typed HTTP clients mostly log locally or return defaults, and no repo-level dashboard/alert configuration was found

### 6) Evidence

- `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs`
- `code\FinanceManager.Infrastructure\Services\Stocks\AlphaVantageClient.cs`
- `code\FinanceManager.Infrastructure\Features\FinancialAccounts\Currencies\Providers\FawazAhmedCurrencyApiClient.cs`
- `code\FinanceManager.Infrastructure\Services\Ai\ServiceCollectionExtension.cs`
- `code\FinanceManager.Api\Features\Maintenance\Controllers\PriceBackfillController.cs`
- `code\FinanceManager.Application\Shared\Maintenance\MaintenanceKeyService.cs`
- `code\FinanceManager.Api\appsettings.json`
- `code\FinanceManager.Api\appsettings.Development.json`
- `code\ServiceDefaults\Extensions.cs`
- `code\AppHost\AppHost.cs`
- `.claude\skills\fm-maintenance\SKILL.md`
