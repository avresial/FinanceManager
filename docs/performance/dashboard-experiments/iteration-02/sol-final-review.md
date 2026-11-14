# Sol final review — iteration 02

**Verdict: stop the latency search now.** No additional experiment is worth doing in this bounded round. E03 has repeatable allocation savings but no replicated primary six-month latency improvement. Do not ship the preserved cache as-is. No page-speed ranking has been established.

Reviewed CLAUDE.md, the retained [iteration-01 review](../iteration-01/sol-recommendations.md), [iteration-02 plan](plan.md), all four A1/B1/A2/B2 articles and full raw JSON reports, captured exits, the preserved E03 patch and current DI/callers. No benchmarks, tests or implementation were run in this review.

## Four-run evidence

Means in milliseconds; managed allocation in bytes/op. Values independently read from `raw/e03-{a1-terra,a2,b1,b2}-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`.

| Window | A1 mean / B | B1 mean / B | A2 mean / B | B2 mean / B |
|---|---:|---:|---:|---:|
| Six months (primary) | 16.153318 / 660362 | 16.249024 / 615588 | 17.381548 / 661840 | 16.290772 / 615042 |
| One month (guardrail) | 14.146717 / 632942 | 13.587870 / 582321 | 14.046273 / 631079 | 13.824295 / 583164 |

Six-month B1 is 0.095706 ms slower than A1 (+0.59%); B2 is 1.090776 ms faster than A2 (−6.28%). A2 itself is 1.228230 ms slower than A1 (+7.60%). Thus the primary timing direction fails replication; pooling baselines would obscure drift. One-month means improve in both pairs (about 3.95% and 1.58%), but that secondary observation cannot rescue the failed primary outcome. This is not proof of zero latency benefit either.

Six-month allocations fall by 44,774 and 46,798 B/op (6.78% and 7.07%); one-month allocations fall by 50,621 and 47,915 B/op (8.00% and 7.59%). This repeated reduction is the credible E03 result. It does not demonstrate reduced GC pauses, throughput gains or user-perceived speed. Within-run confidence intervals and removed outliers are descriptive; CI overlap is not a formal significance test or a substitute for run-level replication.

All four benchmark exit files contain 0. The final restoration test exit is 0; B2 article records 2,253 passing tests after restoration (candidate B1 recorded 2,258). These are retained execution evidence, not tests rerun by this reviewer. Current tracked status contains only the two preexisting GuestAccountSeeder.cs and BenchmarkEnvironment.cs edits; candidate implementation is absent.

## Updated opportunity ranking

This ranks credible practical opportunities, accounting for evidence, cost and correctness. It is explicitly **not an established page-speed ranking**: domain pricing, API overview work and an existing supporting read have incomparable scopes.

| Rank | Experiment | Decision and basis |
|---|---|---|
| 1 | E03 investment valuation reuse | **Conditional allocation-focused follow-up; reject shipping patch as-is.** Approximately 45–51 kB/op saved repeatedly, but primary latency did not replicate. Correctness work is required. |
| 2 | E01 prepared bond history | **Conditional/defer.** Strong domain mechanism (about 31×/178× on the retained long-history fixture), no demonstrated overview timing gain on the current seed. Require evidence of a real long-held-bond workload before API experimentation. |
| 3 | E05 stored bond ID hoist | **Adopt as optional small cleanup**, subject to ordinary parity validation; no urgency or latency claim. Single cold pair showed modest allocation savings and no convincing timing benefit; baseline process exit is missing despite completed reports/logs. No confirmation benchmark now. |
| 4 | E04 liability enumeration reuse | **Reject.** Existing repository caching already avoids the presumed repeated read; mixed timing, flat allocations and added freshness concerns do not justify it. |
| 5 | E02 metadata-free range load | **Reject.** Reversed-order cold comparisons were slower with more allocation; warm evidence offers no convincing gain. Extra cache-key/query work remains an explanation, not measured causality. |
| N/A | E06 batched supporting reads | **Already done.** Present at HEAD, with no before/after counterfactual; no incremental gain to rank or implementation issue needed. |

E01/E05 ordering is opportunity priority, not a measured comparison of their endpoint benefit. E05 is easier to adopt; E01 has larger conditional mechanism potential.

## E03 correctness gate and preferred design

`FinancialAccounts/Registration.cs` registers IAssetsServiceTyped/AssetsServiceInvestment as scoped. `DashboardQueryService` awaits type then account distributions sequentially both for overview creation and cached-overview asset refresh. `AssetsService` enumerates typed services sequentially. `AssetsController` exposes these distributions as read HTTP GET endpoints. These observed paths support reuse in the dashboard composition; they do not universally guarantee read-only or sequential use of a scoped instance.

The preserved dictionary caches account objects and copied values for the entire service lifetime, keyed by user, currency scalar fields and exact timestamp. It has no invalidation after same-scope account/transaction/price changes. New-scope freshness tests do not cover this case. Mutable account objects also mean the tuple is not a fully immutable snapshot. Concurrent calls can both miss, duplicate work and write an unsynchronized Dictionary; sequential current callers are not a concurrency contract. Passing candidate tests does not settle these gaps.

Prefer an explicitly operation-local valuation result shared by the two dashboard distributions: its lifetime matches the read composition, avoiding a general scoped cache and broad invalidation machinery. Preserve existing public behavior and avoid expanding architecture for this optimization. A scoped cache with complete invalidation and synchronization is an alternative only if multiple independent consumers demonstrably need it; it has a larger correctness burden. Separate valuation calls remain preferable when the allocation savings do not justify even the narrow composition change.

## Draft issues — do not publish

1. **E03: reduce dashboard distribution allocation within one read operation — size/M.** Scope: share account load and valuation only within a single overview/refresh composition; no service-lifetime cache. Acceptance: one load/valuation for both distributions, exact user/currency/timestamp isolation, unchanged negative type totals and positive account rows, failed-call retry, fresh subsequent operations after mutation, and no shared mutable cache across concurrent operations. Validation: focused unit tests for both orders, keys, failure/retry, mutation between operations and concurrent operation isolation; overview/refresh parity coverage and repository-required unit checks. At implementation time, one bounded before/after allocation check with unique raw artifacts and exits should verify the redesigned code retains the observed savings without material latency regression. No new latency hunt or browser claim. Reassess size if integration-host changes expand scope.
2. **E01: establish representative long-held-bond overview cost — deferred, size/S for characterization only.** Open only when profiling or portfolio data establishes relevance. Scope: characterize history length and time contribution with anonymized representative workload; no implementation bundled. Acceptance: documented prevalence/cost and explicit go/no-go for the preserved algorithm. Validation: reproducible profile/measurement inputs and attribution, not the domain speedup alone. Any later implementation is a separate sized issue with date/rate/gap parity tests and API measurements.
3. **E05: hoist stable stored bond IDs from the daily loop — size/S.** Optional cleanup scope limited to retained NetWorthService change and its regression test. Acceptance: identical results across duplicate IDs, stored/sold boundaries and date ranges; no change to freshness. Validation: retained targeted parity test plus required unit checks and diff review. No new benchmark required; state the existing single-pair allocation observation and missing baseline exit accurately.

No new E02/E04 issue; E06 needs no implementation issue. None of these follow-ups blocks this experiment delivery.

## Delivery and limits

Measurements cover in-process API work over local SQLite; no HTTP transport, browser rendering, production database or production workload was measured. The earlier iteration-01 E03 artifact collision remains excluded; the clean replacement evidence and this four-run sequence are the admissible evidence. A1 lacks a pre-run hash of the existing source diff, as its article discloses; later preservation evidence is stronger. Do not silently upgrade that assurance.

Mandatory remaining delivery is the parent final report/bundle: link both iterations, articles, patches and raw evidence; distinguish allocation benefit from unproven latency; state restoration and retained final test evidence; preserve unrelated edits and the UI audit. No additional experiment, implementation or review round is mandatory. This review writes only this file and leaves publication/commits to the parent/user authorization.
