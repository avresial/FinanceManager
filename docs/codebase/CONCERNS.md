# Codebase Concerns

## Core Sections (Required)

### 1) Top Risks (Prioritized)

| Severity | Concern | Evidence | Impact | Suggested action |
|----------|---------|----------|--------|------------------|
| high | Development/test secrets and connection details are committed in config files | `code\FinanceManager.Api\appsettings.Development.json`, `code\FinanceManager.Api\appsettings.test.json` | JWT keys and DB connection details are easier to leak or accidentally reuse outside intended environments | Move secrets to User Secrets / environment variables and rotate committed signing keys |
| high | Runtime/database intent is split between PostgreSQL orchestration and SQL Server development config | `code\AppHost\AppHost.cs`, `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs`, `code\FinanceManager.Api\appsettings.Development.json` | Environment drift can break migrations, local repros, and production parity | Decide the primary supported database path and align local config, docs, and CI around it |
| medium | Repo root contains published static site output alongside source | repo root directory listing, `code\` directory listing | Onboarding, scanning, and changes can accidentally target generated artefacts instead of source | Keep `code\` as the documented source of truth and separate deployment artefacts from source if possible |
| medium | Some client-side and controller flows swallow exceptions or return fallback defaults | `code\FinanceManager.Components\Services\LoginService.cs`, `code\FinanceManager.Components\HttpClients\StockPriceHttpClient.cs`, `code\FinanceManager.Api\Controllers\LoginController.cs` | Production failures may be harder to diagnose and can surface as silent false/default states | Standardize error propagation and user-visible failure reporting for external/API calls |
| medium | Large UI code-behind files concentrate multiple responsibilities | `code\FinanceManager.Components\Components\FinancialAccounts\CurrencyAccountComponents\ImportCurrencyEntriesComponent.razor.cs` | Fragile edits and harder review/testing in import-heavy paths | Split import workflows into smaller services/components before adding more complexity |

### 2) Technical Debt

| Debt item | Why it exists | Where | Risk if ignored | Suggested fix |
|-----------|---------------|-------|-----------------|---------------|
| Exchange-rate conversion inside stock-price reads is still uncached | Explicit TODO remains in controller | `code\FinanceManager.Api\Controllers\StockPriceController.cs` | Repeated requests can trigger redundant conversion work and slower responses | Move exchange-rate caching into a reusable service/provider |
| Money-flow labeling logic is unfinished for stock accounts and hardcodes PLN | Explicit TODOs remain in service | `code\FinanceManager.Application\Services\MoneyFlowService.cs` | Incorrect or inconsistent money-flow results for non-PLN or stock-related scenarios | Finish user-currency integration and label support in the service |
| Import UI logic is oversized | Feature growth accumulated in code-behind | `code\FinanceManager.Components\Components\FinancialAccounts\CurrencyAccountComponents\ImportCurrencyEntriesComponent.razor.cs` | Changes are riskier and harder to unit test | Extract parsing, validation, and progress orchestration into smaller services |
| CI solution path is inconsistent with repo instructions | CI uses `./code` and sets `SOLUTION_PATH` to `./code/FinanceManager.slx`, while repo instructions cite `code/FinanceManager.slnx` | `.github\workflows\ci.yml`, `.github\copilot-instructions.md` | Tooling confusion during local/CI troubleshooting | Normalize CI and repo docs to a single solution path |

### 3) Security Concerns

| Risk | OWASP category (if applicable) | Evidence | Current mitigation | Gap |
|------|--------------------------------|----------|--------------------|-----|
| Committed JWT signing keys and dev DB connection string | A05 Security Misconfiguration | `code\FinanceManager.Api\appsettings.Development.json`, `code\FinanceManager.Api\appsettings.test.json` | API project has a `UserSecretsId` and can read env vars | Sensitive defaults are still checked in |
| CORS falls back to `AllowAnyOrigin` when no configured origins are present | A05 Security Misconfiguration | `code\FinanceManager.Api\Program.cs` | Development config provides explicit origins | Missing fail-closed behavior when CORS config is absent |
| JWT bearer metadata validation is relaxed (`RequireHttpsMetadata = false`) | A05 Security Misconfiguration | `code\FinanceManager.Api\Program.cs` | App uses HTTPS redirection | No environment guard was found around that JWT setting |

### 4) Performance and Scaling Concerns

| Concern | Evidence | Current symptom | Scaling risk | Suggested improvement |
|---------|----------|-----------------|-------------|-----------------------|
| Currency exchange-rate history requests issue one HTTP call per day in batches of 50 | `code\FinanceManager.Infrastructure\Services\Currencies\FawazAhmedCurrencyApiClient.cs` | Larger date ranges create many outbound requests | Slow imports/charts and higher dependency sensitivity as date ranges grow | Add caching and/or a bulk-capable provider |
| Stock price controller still performs uncached conversion lookup after fetching price data | `code\FinanceManager.Api\Controllers\StockPriceController.cs` | Repeated price reads can redo conversion work | Hot tickers or dashboard refreshes can amplify latency | Cache converted results or reuse series-level conversion data |
| In-process caches are not shared across instances | `code\FinanceManager.Components\ServiceCollectionExtension.cs`, `code\FinanceManager.Application\Providers\StockPriceProvider.cs` | Cache effectiveness is limited to one browser/server process | Horizontal scaling reduces cache hit rate | Introduce shared/distributed caching for server-side hot paths if multi-instance hosting is expected |

### 5) Fragile/High-Churn Areas

| Area | Why fragile | Churn signal | Safe change strategy |
|------|-------------|-------------|----------------------|
| `code\FinanceManager.Application\ServiceCollectionExtension.cs` | Central dependency-registration hub for many services | high churn in scan output (`21` changes in last 90 days) | Change registrations narrowly and verify downstream composition |
| `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs` | Controls DB provider selection, repositories, external providers, hosted startup behavior | high churn in scan output (`19` changes in last 90 days) | Keep provider wiring isolated and test both DB paths when changing it |
| `code\FinanceManager.Api\Program.cs` | Owns auth, CORS, OpenAPI, hosted services, SignalR, static hosting | high churn in scan output (`12` changes in last 90 days) | Prefer additive changes and review startup side effects carefully |
| `code\FinanceManager.Api\Controllers\StockPriceController.cs` | Crosses repository, provider, currency conversion, admin import flows | high churn in scan output (`10` changes in last 90 days) | Cover changes with both unit and integration tests around stock-price flows |
| `code\FinanceManager.Components\Components\FinancialAccounts\StockAccountComponents\StockAccountDetailsPageContent.razor(.cs)` | High-change UI around stock-account calculations and charts | high churn in scan output (`10` changes each in last 90 days) | Preserve behavior with focused component/service tests and avoid unrelated refactors |

### 6) `[ASK USER]` Questions

1. [ASK USER] Is PostgreSQL via `AppHost` now the intended default local/runtime database, or should SQL Server remain a first-class supported path?
2. [ASK USER] Should the published static site artefacts at the repository root continue to live in this repo, or is `code\` intended to be the only maintained source tree?

### 7) Evidence

- `C:\Users\Miki\.copilot\session-state\56376e31-71d6-4b09-9990-a46379e6c3f3\files\codebase-scan.txt`
- `code\FinanceManager.Api\Program.cs`
- `code\FinanceManager.Api\appsettings.Development.json`
- `code\AppHost\AppHost.cs`
- `code\FinanceManager.Application\Services\MoneyFlowService.cs`
- `code\FinanceManager.Components\Services\LoginService.cs`

