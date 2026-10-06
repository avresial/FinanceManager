# Integrations, stores and operational contracts

[Architecture index](../README.md) · [3. Context and scope](../03-context-and-scope.md) · [8. Crosscutting concepts](../08-crosscutting-concepts.md)

## Integration inventory

Criticality describes the architectural dependency, not an observed availability guarantee. Prices can sometimes be served from stored quotes; AI is optional/configurable.

| System | Purpose and transport | Authentication/configuration | Criticality and evidence |
| --- | --- | --- | --- |
| PostgreSQL / Supabase | Relational system of record; Aspire provisions PostgreSQL locally | Connection string/service binding, provider inferred or configured | High: [database registration](../../../code/FinanceManager.Infrastructure/ServiceCollectionExtension.cs), [AppHost](../../../code/AppHost/AppHost.cs) |
| SQL Server | Supported relational provider alternative | Connection string/provider setting | High when selected: same registration; do not infer the preferred live database from a development default |
| Alpha Vantage | Instrument discovery, stock history and foreign-exchange data over HTTP | Configured API key | High for uncached market data: [AlphaVantageClient](../../../code/FinanceManager.Infrastructure/Features/Assets/Providers/AlphaVantageClient.cs) |
| Fawaz Ahmed Currency API / jsDelivr | Date-versioned currency lookup over HTTP | No API key in the adapter | Medium: [adapter](../../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Currencies/Providers/FawazAhmedCurrencyApiClient.cs) |
| Other currency/instrument providers | Provider-specific FX and identifier resolution paths | Each registered adapter owns its provider configuration | See [infrastructure registration](../../../code/FinanceManager.Infrastructure/ServiceCollectionExtension.cs) for the complete composition; no universal fixed provider order is asserted |
| OpenRouter | AI chat/streaming | Provider API key, model and endpoint settings | Medium: [adapter](../../../code/FinanceManager.Infrastructure/Shared/Ai/OpenRouterChatClient.cs) |
| GitHub Copilot SDK (`GitHub` provider name) | AI chat via SDK-created sessions | Host-side SDK authentication; the adapter creates a CopilotClient and does not implement its own token login | Medium: [adapter](../../../code/FinanceManager.Infrastructure/Shared/Ai/CopilotChatClient.cs); deployment authentication prerequisites must be verified operationally |
| Ollama | Local or remote AI chat endpoint | Configured endpoint; no explicit credential injection in this adapter | Medium: [adapter](../../../code/FinanceManager.Infrastructure/Shared/Ai/OllamaChatClient.cs) |
| LM Studio | OpenAI-compatible AI endpoint | Configured endpoint/model/API key passed to the SDK | Medium: [adapter](../../../code/FinanceManager.Infrastructure/Shared/Ai/LmStudioChatClient.cs) |
| SignalR | Currency import, label-setter progress and admin log events | User JWT and hub-specific authorization; connection token handling is restricted to hub paths | Medium: [API mappings](../../../code/FinanceManager.Api/Program.cs), [security configuration](../../../code/FinanceManager.Api/ApiConfigurationExtensions.cs) |
| MCP / OAuth | Configured clients read owner-isolated financial data through `/mcp`; `/connect/*` supplies authorization | OpenIddict authorization code, PKCE/refresh grants, resource and `mcp` scope checks | High: [MCP feature](../../../code/FinanceManager.Api/Features/Mcp), [deployment requirements](../operations/deployment.md) |
| Maintenance HTTP API | Price backfill and bounded, read-only runtime-log queries | `X-Maintenance-Key` header, hashed DB-managed keys and optional configured fallback | Medium: [maintenance feature](../../../code/FinanceManager.Api/Features/Maintenance), [maintenance usage skill](../../../.claude/skills/fm-maintenance/SKILL.md) |
| Browser local/session storage | Session state, rendered snapshots and expiring data caches | Same-origin browser access; user/context-scoped keys | Medium: [LoginService](../../../code/FinanceManager.Components/Features/Identity/Services/LoginService.cs), [snapshot contracts](ui-snapshots.md) |

## Stores and access boundaries

[AppDbContext](../../../code/FinanceManager.Infrastructure/Persistence/AppDbContext.cs) is the relational system of record for users, accounts, entries, instruments, prices, labels and OAuth state. Infrastructure repositories implement inner-layer contracts; browser components never access the database. OpenIddict entities share this context for clients, scopes, authorizations and token state. Revocation and certificate persistence must remain operational across deployments.

Most hosted integration tests use isolated EF Core InMemory stores, while specific fixtures use SQLite or Docker/PostgreSQL. These are test substitutes, not additional production deployment defaults. See [testing](../quality/testing.md).

Server HybridCache and IMemoryCache accelerate read models/provider work. Browser-local memory and persistent storage accelerate rendering and repeated reads. Current server cache wiring does not establish a shared distributed cache across hosts. [Snapshot guidance](ui-snapshots.md) distinguishes always-refresh rendered state from caches permitted to skip requests.

## Configuration and credentials

Configuration comes from environment-specific appsettings, environment variables, optional ASP.NET User Secrets, and persisted AI-provider settings. Current checked-in host files are `appsettings.Production.json`, `appsettings.Development.json`, and `appsettings.test.json`; the former generic `appsettings.json` path is no longer present. Provider connection selection prioritizes the Aspire `FinanceManagerDb` binding, then `DefaultConnection`, then `FINANCE_MANAGER_DB_KEY`. `OTEL_EXPORTER_OTLP_ENDPOINT` enables the optional telemetry exporter.

Development/test keys and connection defaults are not production credentials. Inspect settings without reproducing their values in documentation; production must provide its required JWT/CORS/proxy settings through secure configuration. AI provider/model/fallback settings are resolved through `IAiConfigurationService`; [FallbackChatClient](../../../code/FinanceManager.Infrastructure/Shared/Ai/FallbackChatClient.cs) orders persisted fallback entries, skips disabled providers/models and chooses registered adapters. Registration order is not a guaranteed OpenRouter → GitHub → Ollama chain.

Maintenance endpoints validate keys in headers, never query strings; keys must not appear in logs or responses. MCP signing/encryption certificate storage, strict redirect registration, proxy settings and disruptive rotation are documented in [deployment](../operations/deployment.md). A general credential-rotation runbook covering every other integration remains a gap.

## Reliability and failure behavior

[ServiceDefaults](../../../code/ServiceDefaults/Extensions.cs) configures the standard HTTP resilience pipeline. Retries are disabled for unsafe HTTP methods. Transient HTTP 408/429/5xx responses, transport errors and timeouts can be retried within the pipeline budget. Defaults allow three retries (four attempts), exponential jitter starting at two seconds, a maximum retry delay of ten seconds, and no Retry-After extension of the configured budget. Configuration bounds `MaxRetryAttempts` and `RetryDelaySeconds`.

Default attempt/total timeouts are 30/90 seconds, capped at 120/300 seconds; circuit-breaker sampling is bounded relative to the attempt timeout. An adapter's own HttpClient.Timeout remains a separate limit. These are code defaults, not proof of the currently deployed settings or an end-to-end latency SLO.

Adapters and provider chains distinguish usable results, explicit failure/empty outcomes and caller cancellation. Caller cancellation must propagate rather than trigger fallback. AI attempts can continue after empty results or provider errors; if no attempt succeeds and any exception was collected, the non-streaming fallback raises AggregateException, including mixed error/empty outcomes. An exhausted chain with no collected exceptions returns an empty response, including when no attempts resolve. Financial callers must not turn unresolved prices/rates into authoritative zeroes. Inspect each adapter/result contract for its exact terminal behavior.

The Fawaz adapter's earliest supported date is `2024-03-02`; earlier requests return OutOfRange before HTTP. Range requests are per-day in bounded batches, rather than a single bulk query. No client API key is required; this does not establish a provider availability or unlimited-rate guarantee.

Maintenance backfill/log endpoints are rate limited (`429`); unavailable/unconfigured maintenance access can return `404`. Log queries support bounded skip/take (`take` at most 200), UTC time ranges, level/text filters, and newest-first ordering. Responses contain items and totalCount, not maintenance credentials.

## Observability and remaining gaps

Shared HTTP diagnostics log safe operation/provider/host/method/path fields for retries, timeouts and outcomes; raw exception text, credentials, secret query strings and financial payloads must not be persisted through those callbacks. Other adapter/application logging paths require their own review: shared filtering alone is not a comprehensive redaction guarantee.

OpenTelemetry covers ASP.NET, HttpClient, runtime metrics, Npgsql and FinanceManager's own activities/meters. Without an OTLP endpoint, exporter persistence is not established. Operator log queries offer bounded incident diagnostics. The repository does not establish a complete production dashboard/alert configuration or uniformly actionable browser error reporting; see [risks](../11-risks-and-technical-debt.md).
