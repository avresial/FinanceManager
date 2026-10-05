# 9. Architecture decisions

[Architecture index](README.md)

The records below reconstruct decisions from the implementation and existing repository guidance on **2026-10-05**. Their status is **reconstructed / implemented**: the design exists, but these records are not original approvals or proof of historical motivation. Each record distinguishes observed behavior from the present-day assessment of consequences and alternatives. Dates identify documentation, not the date the design was first chosen.

| Record | Decision represented by current code | Primary consequences |
| --- | --- | --- |
| [0001 — Layered modular monolith](decisions/0001-layered-modular-monolith.md) | A single API host serves WASM and feature slices behind inward dependency boundaries | Simple deployment/composition and shared database; coupled release and scaling |
| [0002 — Compose external provider fallback](decisions/0002-compose-external-provider-fallback.md) | Provider adapters, configured ordering and explicit outcome/cancellation semantics | Availability options without feature-specific provider wiring; quota, latency and provenance remain concerns |
| [0003 — Separate rendered snapshots and data caches](decisions/0003-separate-ui-snapshots-and-data-caches.md) | Paint last rendered UI state and always refresh; use TTL caching separately | Immediate continuity without skipping freshness; invalidation/context/race discipline is required |
| [0004 — Separate MCP authorization from the SPA session](decisions/0004-separate-mcp-authorization.md) | OAuth scope/resource/reference-token grants secure MCP; tools derive owner identity from the principal | Independent AI-client revocation and identity isolation; extra certificate/grant lifecycle |
| [0005 — Normalize portfolio movements without return semantics](decisions/0005-normalize-portfolio-movements.md) | A shared movement ledger feeds money-weighted return and attribution, with calculations kept in consumer services | Common normalization with explicit signs/FX in consumers; TWR still has a separate path |

## Recording future decisions

Add a numbered ADR when a change materially affects boundaries, persistence/consistency, financial meaning, external contracts, deployment, or a recurring crosscutting pattern. A prospective record should state status, context, the chosen option, supporting evidence, consequences, viable alternatives and when they would become preferable. Link the issue/PR that establishes approval; do not infer approval from the existence of an implementation.

Keep superseded records and link their replacement. Routine coding choices belong in the [conventions](concepts/coding-conventions.md), and a procedure belongs in a runbook. Open decisions such as the supported database baseline belong in [section 11](11-risks-and-technical-debt.md) until resolved. The [maintenance guide](maintenance.md) specifies documentation updates accompanying architectural changes.
