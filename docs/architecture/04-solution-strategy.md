# 4. Solution strategy

[Architecture index](README.md)

## 4.1 Implemented architectural style

FinanceManager is a **layered modular monolith** with feature-grouped folders. One deployable ASP.NET Core API host serves the WASM client and runs HTTP endpoints, SignalR, background jobs, and optional MCP. Business features share an EF Core model and scoped persistence contexts. The browser executes separately on the user's device, while the backend features are not independent deployment units.

The representative interaction is:

```text
Razor component → feature HTTP client → API controller
                 → application service / domain contract
                 → repository or external provider
```

The [actual project-reference diagram](05-building-block-view.md#52-project-reference-view) is the compile-time view; [runtime sequences](06-runtime-view.md) show particular calls. Not every endpoint passes through every box: some controllers call repository contracts directly, some application services implement financial calculations, and authentication startup depends on infrastructure details. Do not add artificial pass-through layers just to match the sketch.

**Inferred rationale:** a single host and shared persistence make cross-feature development and deployment simpler than separate services for a personal-finance application. The project split keeps browser, domain, adapters, and host responsibilities reviewable while feature folders improve navigation. This interpretation is reconstructed from [project definitions](05-building-block-view.md#52-project-reference-view), startup registrations, and [repository guidance](../../CLAUDE.md); it is not a historical decision record. ADRs are maintained in [chapter 9](09-architecture-decisions.md).

## 4.2 Strategy by concern

| Concern | Implemented strategy | Implication / evidence |
|---|---|---|
| Layer composition | Each project exposes grouped `Add*` DI extensions; API composes Application and Infrastructure | Keep lifetime/feature wiring discoverable; [API startup](../../code/FinanceManager.Api/Program.cs), [Application](../../code/FinanceManager.Application/ServiceCollectionExtension.cs), [Infrastructure](../../code/FinanceManager.Infrastructure/ServiceCollectionExtension.cs), [Components](../../code/FinanceManager.Components/ServiceCollectionExtension.cs) |
| Persistence | Repository contracts in Domain, adapters/model in Infrastructure, migrations in API | Business contracts do not require EF; implementation uses one shared model, not isolated feature databases; [AppDbContext](../../code/FinanceManager.Infrastructure/Persistence/AppDbContext.cs) |
| Browser communication | Typed feature clients wrap routes/JSON; complex UI uses code-behind | HTTP and auth behavior are reusable; intended convention is not proof that every file complies; [InvestmentValuationHttpClient](../../code/FinanceManager.Components/Features/FinancialAccounts/HttpClients/InvestmentValuationHttpClient.cs) |
| Price availability | Memory cache, stored quote lookup, provider-selected fetch, normalized quote persistence, FX conversion | Avoid repeated quota use and preserve provenance; failure still can mean missing/partial valuation; [InvestmentPriceProvider](../../code/FinanceManager.Application/Assets/Pricing/InvestmentPriceProvider.cs) |
| Provider variability | Market provider priorities/symbols, ordered FX resolution, configured AI fallback entries | Adapters isolate external protocols; configuration and enabled providers determine actual order; [integrations](concepts/integrations.md) |
| Fast initial rendering | Paint the last rendered snapshot before always fetching; compare rendered content before updating/writing | Failure preserves useful painted data; distinct from a TTL cache that skips requests; [SnapshotRefreshCoordinator](../../code/FinanceManager.Components/Shared/Services/SnapshotRefreshCoordinator.cs) |
| Async work | Hosted services consume in-process channels; job store/status and SignalR expose progress | Request admission and job execution are separate; queue state is not durable across host restart; [background registration](../../code/FinanceManager.Api/ApiConfigurationExtensions.cs) |
| Operational visibility | Shared OpenTelemetry defaults plus feature counters/traces, health endpoints, queued database log persistence | Avoid recursive EF logging and expose host/job health; [telemetry](../../code/FinanceManager.Application/Shared/Diagnostics/FinanceManagerTelemetry.cs), [ServiceDefaults](../../code/ServiceDefaults/Extensions.cs) |
| Boundary verification | ArchUnitNET layer rules, unit/bUnit tests, WebApplicationFactory integration tests | Tests constrain dependencies and selected behaviors, not universal security or performance guarantees; [testing](quality/testing.md) |

## 4.3 Financial calculation separation

Portfolio performance uses a native-currency period ledger to normalize investment buy/sell principal, fees/rebates, and bond contribution/withdrawal changes into positive, typed, dated movements. It separately records trade quantities, including zero-price trades. Minor quote units are normalized to the major currency. The ledger does **not** value positions, perform FX conversion, or decide return signs.

[PortfolioPeriodLedger](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioPeriodLedger.cs) is consumed by separate [money-weighted return](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioMoneyWeightedReturnService.cs) and [return attribution](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioReturnAttributionService.cs) calculations. Those services assign financial meaning to the movement direction and convert historical amounts into their reporting currency. [Period valuation](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance) prices opening/closing holdings separately; [InvestmentValuationService](../../code/FinanceManager.Application/FinancialAccounts/Investments/Valuation/InvestmentValuationService.cs) derives holdings from transactions and prices listings.

**Inferred rationale:** retaining native movements before valuation and return interpretation avoids coupling import/storage semantics to a single performance metric. Verification belongs to the [ledger/return tests](../../code/FinanceManager.Tests.Unit/Application/Services/Investments), including quote-unit, date, fee, and zero-price cases; passing such tests does not establish correctness for every production dataset.

## 4.4 Important nuances and limits

- Application has a project reference only to Domain, but package dependencies and `AddApplication()` include browser authorization/settings abstractions. Components references Application, so the project split is not a strict backend-only application core. Server services still depend on abstractions rather than concrete Infrastructure types.
- API references Infrastructure, WebUi, and ServiceDefaults directly. Its WebUi reference packages the hosted browser client; it is not an HTTP runtime dependency from server logic into Razor pages. AppHost additionally references WebUi at project level, but currently registers only PostgreSQL and API resources.
- Repository caching uses HybridCache decorators and owner-scoped invalidation. Rendered browser snapshots, browser TTL caches, server memory caching, and durable quote storage have different freshness and ownership rules; see [crosscutting concepts](08-crosscutting-concepts.md).
- AI fallback is not a hard-coded OpenRouter → GitHub Models → Ollama sequence. Registered clients are LM Studio, OpenRouter, Copilot, and Ollama; configured enabled provider/model entries and order select attempts. Unavailable providers can still exhaust the chain.
- The former stock-price controller/provider/repository paths no longer describe this checkout. Current investment valuation goes through the asset/listing/quote model documented in [chapter 5](05-building-block-view.md).
- Shared context operations are sequential; valuation batching reduces repeated queries and pricing rather than introducing concurrent operations on one `AppDbContext`.

Current weaknesses and dated audit findings are tracked in [chapter 11](11-risks-and-technical-debt.md). The strategy records what exists; introducing services, another documentation platform, or a new abstraction requires an explicit decision and evidence of benefit.
