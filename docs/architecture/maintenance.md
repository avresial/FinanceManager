# Maintaining the architecture documentation

[Architecture index](README.md)

## Ownership and update triggers

The contributor changing an architectural contract updates its documentation in the same change; the reviewer checks accuracy and scope. The repository maintainer decides unresolved product/operational targets. This is a maintenance convention, not a claim that a dedicated architecture team exists.

| Change | Chapters and supporting material to review |
| --- | --- |
| Purpose, stakeholders or capability scope | 1 and 3 |
| Framework, packages, project references or layer boundaries | 2, 4 and 5; architecture tests; relevant ADR |
| New provider, external contract or trust boundary | 3, 6 and 8; integrations; configuration/deployment; relevant ADR |
| Financial calculation or valuation semantics | 5, 6, 8 and 12; quality scenarios and regression evidence |
| Authentication, ownership, grants or credentials | 3, 6, 7 and 8; OAuth/deployment operations; security quality scenarios |
| Cache, snapshot, invalidation or PWA lifecycle | 6 and 8; snapshot/PWA guides; quality scenarios; relevant ADR |
| Hosting, environment configuration, migrations or recovery | 7; deployment, migration and recovery guides; recovery risks |
| New risk, measured constraint or resolved debt | 10 and 11; cite tests/measurements/issues rather than silently changing historical reports |
| New architecture decision | 9 index and a numbered ADR; affected strategy/concepts/runtime chapters |

## Evidence rules

Link to actual source files using relative Markdown links. A design intention must be labelled as such; do not turn it into an implemented invariant without verification. State the baseline revision/date for audits and measurements. Deployment diagrams describe repository intent unless live hosting was inspected. Test presence is evidence of a check's scope; a passing claim requires an actual run and revision.

Quality scenarios state stimulus, environment, expected response and observable measure. Mark proposed acceptance criteria and unmeasured targets explicitly. Do not infer production latency, capacity, recovery time, accessibility conformance or complete provider parity from unit tests or SQLite microbenchmarks.

Preserve historical reports as evidence. Add new findings to the current risk/quality chapters or a new dated audit; do not rewrite old measurements or mark old findings fixed without evidence. The [migration inventory](migration.md) records superseded claims and the current replacement evidence.

## Decision records

Create `decisions/NNNN-short-decision.md` with context, status/date, decision, alternatives, consequences and source evidence. Reconstructed decisions say that their rationale is inferred from current implementation. New decisions record an actual approved choice. Superseding a decision keeps the old record and links both records; ADR status must not imply deployment or stakeholder approval that has not occurred.

## Review and validation

Before completing a documentation change:

1. Review all affected chapters and supporting guides for agreement with the actual code/configuration.
2. Check local links and anchors, including README/agent instructions, solution documentation entries and code comments. Source `#L` references are GitHub line links; historical line numbers remain historical.
3. Render each changed Mermaid diagram and preview the affected Markdown. Keep diagram labels legible and dependency arrows explicit about compile-time versus runtime meaning.
4. Check that every chapter/support document is reachable from this index or its owner. Reconcile the migration inventory when moving documents; keep operational steps and historical artifacts.
5. Run `git diff --check` and review the full diff. A documentation-only change requires documentation validation; code/configuration behavior changes also require the repository's build, formatting and relevant test gates.

The Markdown source renders directly on GitHub; no generated site, package lockfile or checked-in rendering output is required. Temporary Markdown/Mermaid tooling can be used for review without adding runtime dependencies.
