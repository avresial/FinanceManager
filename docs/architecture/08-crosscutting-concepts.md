# 8. Crosscutting concepts

[Architecture index](README.md)

This section states concepts shared across feature slices. Detailed implementation guides remain under [concepts](concepts/integrations.md), [operations](operations/deployment.md), and [quality](quality/testing.md); those documents are part of this architecture set rather than competing architecture summaries.

## 8.1 Domain and financial semantics

Financial calculations run in Domain and Application; API endpoints enforce transport/security and invoke those services. Amounts, quantities, rates, fees and valuations use `decimal`. Dates are meaningful inputs: a transaction's trade date, a posting date, an observation date, and the user's selected valuation window are not interchangeable.

| Concept | Implemented rule / boundary | Evidence |
| --- | --- | --- |
| Account ownership | User-scoped account selection precedes portfolio calculations; individual endpoint/tool ownership must also be checked | [Financial account repository contract](../../code/FinanceManager.Domain/FinancialAccounts/Shared/Repositories/IFinancalAccountRepository.cs), [MWR service](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioMoneyWeightedReturnService.cs) |
| Listing and price units | Quotes belong to a listing; minor quote currencies such as GBX must be normalized to the major currency using the listing multiplier or the defined minor-unit fallback | [PortfolioPeriodLedger](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioPeriodLedger.cs), [default currencies](../../code/FinanceManager.Domain/FinancialAccounts/Currencies/Entities/DefaultCurrency.cs) |
| Period movement normalization | The ledger records purchase/sale principal, fee/rebate and bond contribution/withdrawal separately. It does not calculate valuation, FX, or returns | [Ledger implementation](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioPeriodLedger.cs) |
| Money-weighted return | Buy/sell cash impact and bond unit changes become signed dated cash flows; opening valuation is negative, ending valuation positive; XIRR calculates annualized return | [MWR orchestration](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioMoneyWeightedReturnService.cs), [XIRR calculator](../../code/FinanceManager.Domain/MoneyFlow/Services/XirrCalculator.cs) |
| Time-weighted return | Daily portfolio values and external flows form return periods; TWR builds pending flows separately from the ledger today | [TWR orchestration](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioTimeWeightedReturnService.cs), [TWR calculator](../../code/FinanceManager.Domain/MoneyFlow/Services/TimeWeightedReturnCalculator.cs) |
| Attribution | Value change is decomposed without overlapping components; known fees are separated and market/valuation effect is the reconciliation residual after currency translation | [Attribution service](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioReturnAttributionService.cs) |
| Missing inputs | Return services have `Unavailable`/`InsufficientData` checks for missing inputs, but scalar price wrappers and the USD numeric fallback can mask upstream FX gaps. These statuses do not guarantee detection of every incomplete or mis-denominated valuation | [Return result types](../../code/FinanceManager.Domain/MoneyFlow/Entities) |
| Bond history | Definitions, entries and opening-boundary history jointly determine valuation; preserve calculation method, capitalization and rounding when optimizing | [Bond dashboard context](../../code/FinanceManager.Application/FinancialAccounts/Bond/Valuation/BondDashboardContext.cs), [performance audit](audits/performance-2026-09-05.md) |

These buy/sell cash-impact semantics are established by the current implementation and its comments referencing issue #729. They are not a complete brokerage cash ledger: do not reinterpret buys as purely internal transfers without a deliberate model change. [ADR 0005](decisions/0005-normalize-portfolio-movements.md) captures the seam and its limits.

FX resolution retains source/status distinctions. [CachedCurrencyExchangeRateSource](../../code/FinanceManager.Application/FinancialAccounts/Currencies/ExchangeRates/CachedCurrencyExchangeRateSource.cs) only caches successful exact stored/provider/same-currency resolutions as successes; carried rates must not masquerade as observations published on that day. A current-day unpublished rate can carry a retry time. Review [integration details](concepts/integrations.md) when changing fallback or historical date behavior.

[InvestmentPriceProvider](../../code/FinanceManager.Application/Assets/Pricing/InvestmentPriceProvider.cs) retains a historical USD numerical fallback when target-currency FX is unavailable; its comments explicitly prohibit caching that fallback under the requested-currency key. Current-day publication-pending returns separately. Therefore, status checks in return services do not alone prove that every positive numeric price is denominated in the requested currency; consumers and fixtures must account for this boundary. [Section 11](11-risks-and-technical-debt.md) records it as a review risk rather than claim the migration fixes it.

## 8.2 Authentication, authorization and tenant isolation

The browser uses a short-lived bearer JWT and a server-set HttpOnly refresh cookie. [LoginService](../../code/FinanceManager.Components/Features/Identity/Services/LoginService.cs) keeps the access token/session in memory, coalesces concurrent refreshes, and clears legacy browser session storage at logout. A script-readable presence cookie is only a restoration hint, not an authentication credential. Cookie-backed refresh/logout requests use antiforgery protection; [AuthController](../../code/FinanceManager.Api/Features/Identity/Controllers/AuthController.cs) owns the server endpoints.

[API security composition](../../code/FinanceManager.Api/ApiConfigurationExtensions.cs) validates issuer, audience, signature and lifetime. Roles protect administrative surfaces; authentication alone does not prove account ownership. Controllers/repositories and tools must derive the effective user from the authenticated principal and reject access to another user's accounts. User identifiers supplied in route/body data are not authority.

Guest sessions have their own lifecycle and optional InMemory sandbox; JWT validation rejects expired guest sessions. `/DevelopLogin/{login}/{page}` is a testing convenience; [DevelopLoginController](../../code/FinanceManager.Api/Features/Identity/Controllers/DevelopLoginController.cs) returns 404 in `Production` and `Release` environments. This is an environment guard, not a branch check. Follow the [usage skill](../../.claude/skills/finance-manager-usage/SKILL.md), and do not treat it as a production authentication mechanism.

MCP has a separate OAuth authorization-code/refresh flow and `mcp` scope/resource policy using OpenIddict reference tokens. A SPA JWT does not itself authorize `/mcp`. [McpUserContext](../../code/FinanceManager.Api/Features/Mcp/McpUserContext.cs) resolves the authenticated subject; [tools](../../code/FinanceManager.Api/Features/Mcp/Tools/FinancialAccountTools.cs) filter ownership and do not accept another user's identity. Disabled MCP returns 404 for its endpoint/discovery surface. Configured redirect URIs and PKCE are reconciled at startup. Normal SPA logout and independent MCP grant revocation have distinct lifecycles; see [ADR 0004](decisions/0004-separate-mcp-authorization.md) and the [deployment runbook](operations/deployment.md).

## 8.3 Caching, rendered snapshots and invalidation

Three mechanisms solve different problems:

| Mechanism | Location | Contract |
| --- | --- | --- |
| Rendered UI snapshot | Browser local storage, `ISnapshotService` / `ISnapshotRefreshCoordinator` | Paint last rendered content promptly, **always** fetch, compare rendered content, skip equal writes, preserve painted content on failure |
| Time-based client data cache | `LocalStorageStateCacheService` and browser state | Reuse fetched data during its validity window; can skip the request; choose explicitly rather than substitute it for a snapshot |
| Server read cache | HybridCache repository/dashboard decorators and exact FX memory cache | Reuse persisted/derived reads; invalidate affected owner scopes on writes; no shared distributed backend is shown in default registration |

The [snapshot guide](concepts/ui-snapshots.md) is the detailed contract. [SnapshotRefreshCoordinator](../../code/FinanceManager.Components/Shared/Services/SnapshotRefreshCoordinator.cs) holds no surface state; the caller owns the version gate. Keys must incorporate user and relevant account/currency/date/filter context. Persist rendered content, not transient selection/paging/expanded state. Storage failures are best effort; a stale asynchronous refresh cannot overwrite a newer context. Diagnostics use snapshot type names instead of owner-bearing keys.

[CacheInvalidator](../../code/FinanceManager.Application/Dashboard/CacheInvalidator.cs) removes `dash:u{userId}` and `global:u{userId}` tags together. Entry repository decorators use owner resolution and point/month-bucket reads; writes must invalidate derived balances as well as entry results. `AddHybridCache()` alone does not establish multi-instance cache consistency. Browser snapshots are local to that browser and must not be treated as server truth or authorization. [ADR 0003](decisions/0003-separate-ui-snapshots-and-data-caches.md) records the rationale and alternatives.

The [PWA guide](concepts/pwa.md) covers installation, service-worker assets and offline shell behavior. Offline/static shell caching does not guarantee offline financial writes or fresh financial data. Program marks service-worker metadata and the manifest `no-cache` so update checks can observe new versions.

## 8.4 Persistence, consistency and background work

EF Core repositories implement Domain contracts. The provider/connection precedence and guest stores are described in [section 7](07-deployment-view.md). Investment writes can use `IAtomicOperation` backed by EF transactions; check the specific use case before assuming an entire workflow is atomic.

Hosted workers consume in-process channels for currency imports, price backfill, insights and labeling. SignalR publishes user/job progress; job stores/channels and guest sessions are process-local unless their specific implementation persists state. A process restart or additional instance can therefore change work/progress behavior. Queued submission is not proof of completed persistence. Inspect cancellation, committed partial work, conflict handling, final recalculation and invalidation before altering import batching.

Apply schema changes using the [migration guide](operations/migrations.md) and use the [recovery runbook](operations/database-recovery.md) for backup/restore. SQLite benchmarks and EF InMemory tests do not establish PostgreSQL/SQL Server migration compatibility.

## 8.5 Provider coordination and resilience

Provider adapters belong to Infrastructure and are accessed through application/domain contracts. [Integration details](concepts/integrations.md) list configuration and external boundaries. AI fallback is dynamically configured rather than universally fixed to one provider order. Registered adapters include LM Studio, OpenRouter, GitHub Copilot SDK (`GitHub` provider) and Ollama; stock providers include Alpha Vantage, Twelve Data and EODHD; official/general FX sources have their own ordered resolution chain.

[ServiceDefaults](../../code/ServiceDefaults/Extensions.cs) supplies bounded HTTP attempt/total timeouts, retries with exponential backoff and jitter, a circuit breaker and service discovery. Unsafe HTTP methods have retry disabled by default. Provider-local quotas, timeouts, symbol mapping, capability checks and explicit provider selection still matter. [FallbackStockPriceSource](../../code/FinanceManager.Application/FinancialAccounts/Stock/Pricing/FallbackStockPriceSource.cs) orders providers by priority, falls through on empty/error/timeout, and propagates caller cancellation; requested-provider selection is a separate path. AI exhausted-fallback behavior can produce an aggregate failure or empty response depending on outcomes. Neither fallback nor retry guarantees that data exists or that services are available. See [ADR 0002](decisions/0002-compose-external-provider-fallback.md).

## 8.6 Errors, diagnostics and configuration

[GlobalExceptionHandler](../../code/FinanceManager.Api/Shared/Middleware/GlobalExceptionHandler.cs) and ProblemDetails provide a consistent unhandled API failure envelope. Feature-specific result/status models distinguish expected cases such as insufficient financial data, rate-limited login, ownership failures and import conflicts. Do not replace actionable failures with a success-shaped zero/default. Client refresh and snapshot paths intentionally preserve usability on recoverable failures and log diagnostics; that is different from claiming fresh results were obtained.

OpenTelemetry instruments ASP.NET Core, HTTP clients, runtime metrics, Npgsql and FinanceManager business diagnostics. Export requires the configured OTLP endpoint. Database-backed logs travel through a queue/persistence worker and retention worker; Program filters EF/SignalR/connection noise from the sink to prevent recursion. HTTP resilience callbacks redact dependency diagnostics; tokens, provider keys, raw sensitive payloads and owner-bearing cache keys should not be logged. Admin log/progress/detail endpoints remain authorization boundaries.

Configuration is composed from environment settings, secrets and options, with startup validation for security-sensitive values. Production startup must fail for missing required secrets/allowlists; local insecure defaults are explicitly Development-only. Never copy local/test seeding credentials into hosted settings. Default settings and configured provider priorities are evidence of intent, not a description of deployed secret values.

## 8.7 Development conventions and evolution

Each layer wires its services through its composition extensions. Feature-oriented namespaces and folders group slices inside the established layers. Razor components use typed clients; Domain must stay independent of EF/ASP.NET, and browser projects must stay independent of Infrastructure. [Architecture tests](../../code/FinanceManager.Tests.Architecture/LayerDependencyTests.cs) enforce these constraints against compiled assemblies.

Follow [coding conventions](concepts/coding-conventions.md), [testing guidance](quality/testing.md), [CLAUDE.md](../../CLAUDE.md) and the [documentation maintenance guide](maintenance.md) when evolving the system. Add a prospective ADR for significant boundary/consistency/deployment decisions; these reconstructed ADRs must not be mistaken for original design approvals.

### Color usage

The UI palette is defined in [`App.razor`](../../code/FinanceManager/App.razor); `Secondary` stays amber, close to `Primary`, so amber must signal "important or clickable".

- **Accent (amber, `Primary`/`Secondary`):** actions (buttons, links), the selected/active state, and at most one key figure per card.
- **Muted text:** descriptions, subtitles, footnotes, period labels, empty-state hints and their decorative icons use the `mud-text-secondary` class (`var(--mud-palette-text-secondary)`), not `Color.Secondary`.
- **Semantic colors (`Success`, `Error`, `Warning`):** gains, losses and alerts only.

Both themes must keep muted text readable; production renders dark, local sandboxes render light.
