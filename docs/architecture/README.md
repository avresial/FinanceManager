# FinanceManager architecture — arc42

FinanceManager is a personal finance application with a Blazor WebAssembly browser client and one ASP.NET Core API host. This documentation follows the [arc42 template](https://arc42.org/overview/) using multiple Markdown chapters, source-linked evidence and Mermaid diagrams. It documents the implemented layered modular monolith, its financial semantics and its operational boundaries.

Baseline: repository `develop` at `61a36c9c`, inspected on **2026-10-05** for [issue #888](https://github.com/avresial/FinanceManager/issues/888). Source/configuration evidence establishes repository behavior and intended deployment; it does not attest to live Azure settings, current production measurements or completed historical audit remediation.

## The twelve sections

| Section | Read it to understand |
| --- | --- |
| [1. Introduction and goals](01-introduction-and-goals.md) | Purpose, stakeholders, capabilities and architectural quality priorities |
| [2. Architecture constraints](02-architecture-constraints.md) | Platform, tools, dependency boundaries and imposed limitations |
| [3. Context and scope](03-context-and-scope.md) | External actors, integrations, interfaces and trust boundaries |
| [4. Solution strategy](04-solution-strategy.md) | How the architecture addresses the goals and constraints |
| [5. Building block view](05-building-block-view.md) | Projects, actual dependencies, feature responsibilities and extension points |
| [6. Runtime view](06-runtime-view.md) | Representative synchronous, asynchronous and failure flows |
| [7. Deployment view](07-deployment-view.md) | Local and hosted topology, configuration and operational recovery |
| [8. Crosscutting concepts](08-crosscutting-concepts.md) | Financial semantics, identity, caching, resilience and diagnostics |
| [9. Architecture decisions](09-architecture-decisions.md) | Reconstructed ADRs, alternatives and consequences |
| [10. Quality requirements](10-quality-requirements.md) | Verifiable quality scenarios, available evidence and validation gaps |
| [11. Risks and technical debt](11-risks-and-technical-debt.md) | Current risks, historical concern disposition and unanswered decisions |
| [12. Glossary](12-glossary.md) | Shared technical and financial vocabulary |

## Reading paths

New contributors: sections 1 → 3 → 4 → 5 → 6, then [testing](quality/testing.md) and [coding conventions](concepts/coding-conventions.md).

Feature changes: relevant building blocks and runtime scenarios, section 8's contracts, then section 10's verification requirements. For snapshots, read the full [snapshot guide](concepts/ui-snapshots.md) before adding state persistence.

Operators: section 7 → [deployment and MCP configuration](operations/deployment.md) → [database backup/restore/rollback](operations/database-recovery.md). Run operational commands only with the required access and an approved change/recovery plan; documentation migration does not execute them.

Architecture review: sections 4, 9, 10 and 11. ADRs identify reconstructed rationale explicitly; proposed quality targets are not measured guarantees.

## Supporting documentation by section

| Owner | Detailed documents |
| --- | --- |
| 7. Deployment | [Deployment](operations/deployment.md), [EF migrations](operations/migrations.md), [database recovery](operations/database-recovery.md) |
| 8. Concepts | [Integrations](concepts/integrations.md), [UI snapshots](concepts/ui-snapshots.md), [PWA caching/offline/update behavior](concepts/pwa.md), [coding conventions](concepts/coding-conventions.md), [transaction automation mockup](concepts/mockups/transaction-automation-inline.svg) |
| 10. Quality | [Testing](quality/testing.md), [benchmark operation and interpretation](quality/benchmarks.md), [historical accessibility audit](audits/accessibility-audit.md), [historical performance audit](audits/performance-2026-09-05.md) |
| 11. Risks/debt | [Historical backend cleanup audit](audits/backend-cleanup.md), [historical .NET upgrade assessment](audits/dotnet10-upgrade/assessment.md) and its preserved [CSV](audits/dotnet10-upgrade/assessment.csv)/[JSON](audits/dotnet10-upgrade/assessment.json) |

[Migration inventory](migration.md) records every pre-migration document and its disposition. [Maintenance rules](maintenance.md) explain how to update chapters, decisions and evidence together. The root [README](../../README.md) remains the product/onboarding entry point, [CHANGELOG](../../CHANGELOG.md) remains release history, and [CLAUDE](../../CLAUDE.md)/[AGENTS](../../AGENTS.md) and tool skills remain contributor/agent instructions.

## Template attribution

Structure adapted from the [arc42 template](https://arc42.org/download/), created by Peter Hruschka and Gernot Starke. Template material is available under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). The repository's own architecture narrative and source remain subject to its existing licensing; this attribution does not relicense the application.
