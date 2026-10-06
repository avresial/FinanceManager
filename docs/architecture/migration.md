# Documentation migration inventory

[Architecture index](README.md)

Migration for [issue #888](https://github.com/avresial/FinanceManager/issues/888), baseline `develop` / `61a36c9c`, 2026-10-05. All 27 tracked pre-migration Markdown files are accounted for below. Technical documents are consolidated into chapters or moved to section-owned supporting guides. Repository/tool entry points keep their required discovery locations and link into the architecture set. No technical migration item is deferred.

## Migrated technical documents

Old paths below are identifiers for the migration, not live links. Supporting guides preserve their operational detail; consolidated chapters correct superseded source paths and claims rather than retaining contradictory legacy pages.

| Previous document | Destination / arc42 owner | Disposition |
| --- | --- | --- |
| `docs/codebase/ARCHITECTURE.md` | [4. Strategy](04-solution-strategy.md), [5. Building blocks](05-building-block-view.md), [6. Runtime](06-runtime-view.md), [8. Concepts](08-crosscutting-concepts.md), [11. Risks](11-risks-and-technical-debt.md) | Architectural style, flow, responsibilities, patterns, ledger seam, risks and evidence consolidated and verified |
| `docs/codebase/STACK.md` | [2. Constraints](02-architecture-constraints.md), [7. Deployment](07-deployment-view.md), [testing](quality/testing.md) | Runtime, frameworks/package source, toolchain, commands, configuration and deployment constraints consolidated; stale examples corrected |
| `docs/codebase/STRUCTURE.md` | [5. Building blocks](05-building-block-view.md), [2. Constraints](02-architecture-constraints.md) | Repository map, entry points, module boundaries, naming and organization consolidated using current feature folders |
| `docs/codebase/CONCERNS.md` | [11. Risks and technical debt](11-risks-and-technical-debt.md) | Every original risk/debt/security/performance/churn topic and open question has a current or historical disposition |
| `docs/codebase/CONVENTIONS.md` | [Coding conventions](concepts/coding-conventions.md), section 8 | Naming, formatting, imports, boundaries, errors/logging and testing conventions retained and refreshed |
| `docs/codebase/INTEGRATIONS.md` | [Integrations](concepts/integrations.md), sections 3/8 | Integration/store inventory, secrets, resilience, observability and gaps retained with current adapters/configuration |
| `docs/codebase/TESTING.md` | [Testing](quality/testing.md), section 10 | Commands, layout, scope matrix, mocks/isolation, coverage and gaps retained with current runner/provider details |
| `docs/codebase/UI-SNAPSHOTS.md` | [UI snapshots](concepts/ui-snapshots.md), section 8 | Full workflow, sample, key/equality/failure/state rules and caller guidance retained |
| `docs/PWA.md` | [PWA](concepts/pwa.md), section 8 | Audit history, worker behavior, filtering, offline scope, update lifecycle and verification retained |
| `docs/deployment.md` | [Deployment](operations/deployment.md), section 7 | Hosted setup, secrets, MCP/proxy/certificate lifecycle, health probes, tier caveats and preview status retained |
| `RUNBOOK.md` | [Database recovery](operations/database-recovery.md), section 7 | Backup/restore/rollback alternatives, commands, preconditions, verification and decision guide retained |
| `Migrations.md` | [Migrations](operations/migrations.md), section 7 | PowerShell migration commands and recovery references retained; example migration names generalized |
| `code/FinanceManager.Benchmarks/README.md` | [Benchmarks](quality/benchmarks.md), section 10 | Full commands, endpoint coverage, exclusions, metrics/caveats, environment switches, harness and recalculation note retained; project README becomes a pointer |
| `docs/accessibility/ACCESSIBILITY-AUDIT.md` | [Accessibility audit](audits/accessibility-audit.md), section 10 | Original report retained with historical provenance; no new conformance claim |
| `docs/performance/OPTIMIZATION-AUDIT-2026-09-05.md` | [Performance audit](audits/performance-2026-09-05.md), section 10 | Original dated opportunities, evidence, recommendations and validation requirements retained; source links rebased |
| `Cleanup.md` | [Backend cleanup audit](audits/backend-cleanup.md), section 11 | Original findings, completed labels and follow-up recommendations retained as historical evidence |
| `.github/upgrades/assessment.md` | [Upgrade assessment](audits/dotnet10-upgrade/assessment.md), section 11 | Original generated .NET 9 baseline report and diagrams retained; explicitly not the current platform |

The upgrade's [CSV](audits/dotnet10-upgrade/assessment.csv) and [JSON](audits/dotnet10-upgrade/assessment.json) companions and the [transaction automation SVG](concepts/mockups/transaction-automation-inline.svg) are moved intact. They are supporting artifacts, not additional chapter sources.

## Retained repository and tool entry points

| Document | Reason / architecture relationship |
| --- | --- |
| [README.md](../../README.md) | Product features, screenshots and quick start remain the onboarding entry point; detailed architecture now links to the index |
| [CHANGELOG.md](../../CHANGELOG.md) | Release history stays canonical and unchanged; historical issue links are not rewritten |
| [AGENTS.md](../../AGENTS.md) | Agent discovery contract and lifecycle instructions stay in place; canonical architecture index added |
| [CLAUDE.md](../../CLAUDE.md) | Contributor/build/branch conventions stay in place; overlapping architectural narrative replaced by links and mandatory boundary reminders |
| [Copilot instructions](../../.github/copilot-instructions.md) | Tool discovery contract stays in place with a canonical architecture pointer |
| [Changelog skill](../../.claude/skills/changelog/SKILL.md) | Tool workflow instructions stay in place; not release or architecture narrative |
| [Usage skill](../../.claude/skills/finance-manager-usage/SKILL.md) | App-use/test-login contract stays in place; referenced from runtime/quality material |
| [UI testing skill](../../.claude/skills/ui-testing/SKILL.md) | Rendering/screenshot workflow stays in place; referenced by quality documentation |
| [MudBlazor skill](../../.claude/skills/mudblazor/SKILL.md) | UI tool conventions stay in place; linked as contributor guidance |
| [Maintenance skill](../../.claude/skills/fm-maintenance/SKILL.md) | Runtime maintenance workflow stays in place; linked from integration/operational documentation |

Retaining these tool entry points preserves agent behavior and contributor discovery. Their instructions are not copied into an independently maintained arc42 version.

## Verified corrections and preservation boundaries

- Current investment pricing uses InvestmentValuation/InvestmentPriceProvider/PriceQuoteRepository. Former StockPriceController/StockPriceProvider/StockPriceRepository examples are superseded; feature folders and current assemblies are linked instead.
- AI registration includes LM Studio, OpenRouter, Copilot SDK and Ollama. Persisted enabled provider/model/fallback entries determine attempt order; a fixed OpenRouter → GitHub Models → Ollama chain is not asserted.
- Current host config files are environment-specific; absent generic appsettings paths are replaced with real evidence. Database support is distinguished from a verified live-host choice.
- Source is under `code/`; former claims about published static assets/sample-data at the repository root are not facts about this baseline.
- Tests use Microsoft.Testing.Platform from `code/` with `--project`. Old bare-path/VSTest coverage examples are superseded. Relational SQLite and Docker/PostgreSQL fixtures are acknowledged without claiming full provider parity.
- Historical CORS/JWT/proxy/CI-path concerns are assessed against present configuration; old scan counts and absolute developer-machine paths are historical evidence, not current measurements.
- Snapshot freshness means an always-attempted refresh; failed refreshes may keep stale content. PWA network bypass does not override other data-cache freshness rules.
- The database recovery runbook clarifies stopping failed artifacts and starting a schema-compatible prior artifact after rollback/restore; startup auto-migration must not undo recovery. Its Supabase scope is an assumed operational target, not live-host attestation.
- Historical audits preserve findings/line numbers/statuses/measurements from their own baseline. Rebased links lead to current source; they do not certify old line numbers or remediation status.

## Three-pass acceptance

1. **Factual baseline:** current project references, startup/security/provider wiring, financial flows, tests and CI deployment intent are linked in chapters and detailed guides. Unsupported historical claims are separated from current facts.
2. **Architecture narrative:** all twelve sections are populated; context, actual dependencies, deployment and runtime diagrams connect the narrative. All seventeen technical Markdown sources and their assets are migrated, with updated entry points and code-comment references.
3. **Rationale and maintenance:** section 9 links reconstructed ADRs with evidence/alternatives/consequences; section 10 distinguishes targets from proof; section 11 retains risk dispositions/open questions; section 12 defines domain terms. [Maintenance rules](maintenance.md) define update triggers and review checks.

Validation evidence is reported in the issue work log and implementation handoff. The presence of this inventory alone is not proof that the links, rendered diagrams or source assertions passed review.
