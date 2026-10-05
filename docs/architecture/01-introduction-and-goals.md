# 1. Introduction and goals

[Architecture index](README.md)

## 1.1 Requirements overview

FinanceManager is a personal finance application: users record financial accounts and transactions, import their history, and inspect balances, cash flow, investment performance, recurring payments, and generated insights. The browser client runs Blazor WebAssembly; an ASP.NET Core host serves the client and exposes the API, background processing, and optional MCP endpoint. This is a layered modular monolith, not a collection of independently deployed business services. See the [building blocks](05-building-block-view.md) and [deployment view](07-deployment-view.md).

The following capabilities are evidenced by the current feature folders and registrations. A capability being implemented does not imply that every provider is configured or that every production environment enables it.

| Capability | Architectural significance | Implementation evidence |
|---|---|---|
| Currency, investment, and bond accounts | Account ownership, historical entries, trade quantities, and account-specific valuation must remain coherent | [Domain financial accounts](../../code/FinanceManager.Domain/FinancialAccounts), [application financial accounts](../../code/FinanceManager.Application/FinancialAccounts) |
| Dashboard and portfolio analytics | Combine historical transactions, position valuations, currency conversions, and selected reporting periods | [Dashboard application services](../../code/FinanceManager.Application/Dashboard), [portfolio performance](../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance), [money flow](../../code/FinanceManager.Application/MoneyFlow) |
| Import, conflict resolution, and instrument discovery | Validate imported records, preserve user ownership, and report long-running work | [Currency import](../../code/FinanceManager.Application/FinancialAccounts/Currencies/Import), [instrument discovery](../../code/FinanceManager.Application/FinancialAccounts/Investments/Discovery) |
| Labels, transaction rules, recurring payments, and alerts | Derive classifications and actionable information from the user's financial history | [Labels](../../code/FinanceManager.Application/Labels), [transaction rules](../../code/FinanceManager.Application/TransactionRules), [alerts](../../code/FinanceManager.Application/Alerts) |
| Optional AI insights and categorization | Provider configuration, fallback, and background processing surround generated output | [Insights](../../code/FinanceManager.Application/Insights), [AI adapters](../../code/FinanceManager.Infrastructure/Shared/Ai) |
| Identity, settings, administration, and demo access | Distinguish registered users, administrators, and guest sandboxes | [Identity API](../../code/FinanceManager.Api/Features/Identity), [administration API](../../code/FinanceManager.Api/Features/Administration) |
| OAuth-protected MCP tools | Give configured external clients authenticated access to user-scoped tools | [MCP feature](../../code/FinanceManager.Api/Features/Mcp/McpFeatureExtensions.cs) |

The [repository README](../../README.md) is the product entry point. This architecture documentation explains implementation structure, constraints, runtime behavior, decisions, and operational consequences; it does not repeat the product screenshot gallery or change history.

## 1.2 Quality goals

These priorities are proposed architecture goals derived from the application's handling of financial and identity data. They are not approved service-level objectives or measured production guarantees. Chapter [10](10-quality-requirements.md) gives concrete scenarios and evidence boundaries.

| Priority | Goal | Why it matters | Existing mechanism |
|---|---|---|---|
| 1 | Correct and explainable financial calculations | Amount, currency, date, quote unit, and cash-flow direction together determine meaning | Separate period ledger, valuation, money-weighted return, and attribution services; [performance tests](../../code/FinanceManager.Tests.Unit/Application/Services/Investments) |
| 2 | Restrict financial data to its owner | Authentication alone must not permit access to another user's account or import job | Controller ownership checks, user-scoped repositories, MCP identity context, and authorized SignalR groups; [runtime view](06-runtime-view.md) |
| 3 | Preserve useful state through failures | Market-data outages, delayed FX publication, and failed refreshes should have explicit behavior | Provider fallback, result statuses, stored quotes, and rendered UI snapshots; [solution strategy](04-solution-strategy.md) |
| 4 | Keep changes maintainable and reviewable | A small layered application benefits from clear boundaries and source-linked rationale | Project separation, feature folders, central dependencies, architecture tests, and this arc42/ADR structure |
| 5 | Respond usefully while fresh data loads | Financial dashboards and imports should communicate loading, progress, and failure without discarding an already rendered view | Snapshot refresh coordinator, version gates, channels, SignalR, and status polling |

## 1.3 Stakeholders

Roles below are inferred from the code's responsibilities; they do not identify particular people or formal organizational ownership.

| Stakeholder role | Interest | Documentation entry points |
|---|---|---|
| Registered user | Accurate personal records, private data, understandable analysis, recoverable import mistakes | [Context](03-context-and-scope.md), [quality requirements](10-quality-requirements.md) |
| Guest/demo user | Explore the app with temporary sample data and no persistent user session | [Identity runtime](06-runtime-view.md#64-browser-login-and-token-refresh), [crosscutting concepts](08-crosscutting-concepts.md) |
| Developer/reviewer | Locate the right feature and layer, preserve behavior, and verify a change | [Constraints](02-architecture-constraints.md), [building blocks](05-building-block-view.md), [testing](quality/testing.md) |
| Operator/administrator | Configure integrations, inspect health and logs, deploy, and recover data | [Deployment](07-deployment-view.md), [deployment guide](operations/deployment.md), [database recovery](operations/database-recovery.md) |
| MCP client integrator | Configure OAuth, understand enabled endpoints and user scope | [Technical context](03-context-and-scope.md#32-technical-context), [MCP runtime](06-runtime-view.md#65-oauth-authorized-mcp-tool-call), [integrations](concepts/integrations.md) |

## 1.4 Documentation scope and confidence

Chapters describe the repository implementation inspected for issue #888. Source links are the evidence for current behavior. Historical audits remain dated observations, architectural rationale reconstructed from code is labeled as inference, and new goals are labeled as proposed. Production topology and provider availability cannot be proven from committed configuration alone. Open risks and verification gaps belong in [chapter 11](11-risks-and-technical-debt.md).
