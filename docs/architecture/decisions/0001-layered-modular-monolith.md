# ADR 0001: Use a layered modular monolith with a hosted WASM client

[Decision index](../09-architecture-decisions.md) · [Architecture index](../README.md)

- **Status:** Reconstructed / implemented
- **Documented:** 2026-10-05
- **Original approval/date:** Not established by this record

## Context and observed decision

FinanceManager delivers budgeting, account history, imports, portfolio calculations and administrative operations through one ASP.NET Core API. The API hosts the Blazor WebAssembly static client and workers. Feature slices are organized inside Domain, Application, Infrastructure, Api and Components; repository/service contracts point inward rather than making Domain depend on EF or HTTP hosting.

The observed decision is to keep this **layered modular monolith** and its typed browser-client boundary. [CLAUDE.md](../../../CLAUDE.md) explicitly names the architecture and constraints. [API Program](../../../code/FinanceManager.Api/Program.cs), [project references](../../../code/FinanceManager.slnx), [layer tests](../../../code/FinanceManager.Tests.Architecture/LayerDependencyTests.cs) and the [deployment workflow](../../../.github/workflows/ci.yml) provide implementation evidence. Domain calculations and Application orchestration remain separate from transport and concrete persistence/provider adapters.

## Consequences and present-day assessment

One artifact/composition root keeps local operation and deployment simple. Inward contracts enable calculation testing without a database and swapping adapters. Feature folders retain coherent change scopes without introducing a service network.

The host, schema, workers and browser asset release remain coupled. HTTP/persistence/provider wiring shares startup, and process-local job/cache/session state constrains independent scale. Architectural tests enforce dependency rules; they cannot alone establish good feature boundaries or prevent every domain calculation from leaking into a controller.

These are consequences of the current design, **not an assertion of the original authors' motivation**.

## Alternatives and when preferable

| Alternative | Trade-off | Prefer when |
| --- | --- | --- |
| Independently deployed services | Independent ownership/scaling, but introduces distributed consistency, deployment, authentication and observability costs | Evidence shows an independently owned capability with distinct scaling/release needs that outweigh those costs |
| Separate frontend hosting/BFF | Can isolate asset delivery or add server-side browser session handling, but requires another deployment/security boundary | Frontend delivery, security or team ownership requirements justify it |
| A flatter monolith | Fewer projects initially, but weaker dependency boundaries and more coupling between calculations and infrastructure | A much smaller throwaway application has no durable domain/testing needs |

## Review triggers

Revisit when measured workload requires independent worker/API scaling, multiple instances must share state, separate release ownership becomes necessary, or browser/backend dependency leakage persists despite existing boundaries. A prospective ADR should record the approved change and transition plan; this reconstruction does not authorize a redesign.
