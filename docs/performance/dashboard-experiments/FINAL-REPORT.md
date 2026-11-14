# Dashboard performance investigation — final report

> **Update — long-held-bond rerun:** E01 was retested on a synthetic 10-year-held bond after this report was first written. Two cold API pairs showed substantial and repeatable timing/allocation reductions with full candidate/baseline response parity. [Read the confirmation](iteration-03/e01-long-bond-confirmation.md). The earlier E01 deferral and “no candidate” statements below describe the initial dataset and are superseded for that specific workload. [Issue #1028](https://github.com/avresial/FinanceManager/issues/1028) was filed. E03 and E05 were filed as [#1026](https://github.com/avresial/FinanceManager/issues/1026) and [#1027](https://github.com/avresial/FinanceManager/issues/1027).

## Decision

Stop experiments for now. No temporary candidate established a repeatable improvement in dashboard page speed. E03 repeatedly reduced server allocations but failed the primary latency replication check. Do not ship its scoped cache as-is. Keep the existing layered monolith and sequential database access.

Six experiments were assessed, followed by a bounded four-run E03 confirmation and two Sol reviews. All temporary production changes are restored. No production optimization, merge, push, or issue publication was performed.

## Recommendations, in priority order

These are ranked practical opportunities, not a statistically established page-speed league table; the scopes differ.

| Priority | Candidate | Recommendation | Evidence |
|---|---|---|---|
| 1 | E03 investment valuation reuse | Consider a narrow allocation-focused implementation issue; do not adopt the preserved service cache | Repeated ~45–51 kB/request savings. Primary six-month latency changed direction between pairs. |
| 2 | E01 prepared bond history | Defer until a representative long-held-bond workload establishes API cost | Domain fixture improved about 31×/178× for 31/183 dates, but no demonstrated overview latency benefit. |
| 3 | E05 precomputed bond IDs | Optional small cleanup, not a speed claim | One pair saved ~44 kB/16 kB for six/one months; latency inconclusive. |
| 4 | E04 shared liability input list | Reject | Existing repository already caches reads; mixed timings and essentially flat allocations. |
| 5 | E02 metadata-free net-worth reads | Reject | Cold timings worsened in both run orders, with higher allocations. |
| — | E06 transaction-log batching | Already implemented; no change needed | Current endpoint measured ~6.664 ms, with no before/after counterfactual. |

## E03 confirmation

A = baseline, B = unchanged candidate. Four sequential warm runs used identical seed data and separate artifact directories. Times are milliseconds; allocations are managed bytes per operation. These are in-process API measurements, not browser timings.

| Run | Six-month mean | Six-month B/op | One-month mean | One-month B/op |
|---|---:|---:|---:|---:|
| A1 | 16.153 | 660,362 | 14.147 | 632,942 |
| B1 | 16.249 | 615,588 | 13.588 | 582,321 |
| A2 | 17.382 | 661,840 | 14.046 | 631,079 |
| B2 | 16.291 | 615,042 | 13.824 | 583,164 |

The primary six-month result was 0.59% slower in B1 versus A1, then 6.28% faster in B2 versus A2. Baseline drift prevents a general latency claim. One-month means improved in both pairs, but this secondary observation does not establish the primary claim. Allocation reductions repeated: 6.78–7.07% for six months and 7.59–8.00% for one month. This does not prove GC-pause, throughput or perceived-speed gains. Within-run confidence intervals are not a run-level significance test.

## Architecture and correctness

Prefer an operation-local result shared by the two dashboard asset distributions if E03 is implemented later. That matches the actual overview/refresh composition and avoids service-lifetime invalidation. Current callers are sequential read paths, but that is not a universal guarantee for a scoped service. The preserved patch stores mutable account references and has no same-scope write invalidation or concurrent-access contract.

A scoped cache with complete invalidation and synchronization is a larger alternative only if independent consumers demonstrably need it. Keeping separate calls remains reasonable if the allocation savings do not justify a narrow composition change. Parallelizing the shared EF context is not an option.

## Proposed issues — drafts only

1. **Reduce dashboard distribution allocation within one read operation — size/M.** Share valuation only within an overview/refresh operation. Acceptance: one load/valuation for both distributions; preserve user/currency/exact timestamp isolation, negative type totals and positive account rows; failed calls retry; subsequent operations after writes are fresh; concurrent operations share no mutable cache. Validate focused parity, failure, mutation-between-operations and concurrency-isolation tests, required unit suite, and one bounded allocation comparison for the redesigned implementation. No page-speed promise.
2. **Characterize representative long-held-bond overview cost — deferred, size/S.** Open only after real profiling or portfolio distribution establishes relevance. Document prevalence and attributed cost, then make an explicit go/no-go decision. An implementation would be a separate issue with rate/date/gap parity and API measurements. The synthetic mechanism win alone is insufficient.
3. **Hoist stable stored bond IDs from the daily loop — optional, size/S.** Limit scope to the preserved E05 local change and regression coverage. Require identical values across duplicate IDs, boundary-only instruments, accounts and date ranges. Validate required unit tests and diff review; no new benchmark hunt is needed.

No E02/E04 implementation issue and no duplicate E06 issue are proposed. None blocks this delivery.

## Validation and final state

- Final restored unit suite: **2,253 passed, 0 failed, exit 0**. [Log](iteration-02/raw/e03-final-restoration-tests.log), [exit](iteration-02/raw/e03-final-restoration-tests.exit).
- E03 candidate B1: 2,258 unit tests passed. All four confirmation benchmark exit files contain 0 and each run completed both windows.
- Temporary candidate code and candidate-specific tests are retained in patches, not left applied. Earlier retained benchmark/routing coverage is committed at `8f987e2e`; date-order parity coverage at `f3945df5`.
- The two preexisting source edits remain: GuestAccountSeeder.cs and BenchmarkEnvironment.cs. They were held fixed, not included as optimization gains. No broad cleanup was performed; UI audit ownership was untouched.
- Seed SHA256: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`. Baseline HEAD: `8f987e2e5f909716ece1d396f96c2701fbc3707a`.

## Limits and stopping rationale

SQLite, offline providers and in-process API hosting exclude browser rendering, transport, production database behavior and real workload distribution. Most first-round candidates have only one pair. E03's collided early artifacts were preserved but excluded; replacement runs and the four-run sequence are authoritative. A1 lacks a pre-run hash of unrelated edits; later runs have hash evidence. E05's first baseline exit was not captured, although two completed benchmarks and reports exist. An unrelated UI timeout during E04 validation passed on one retry; both logs are retained.

No further experiment is warranted now: E03's bounded latency confirmation failed replication; E02/E04 do not justify their changes; E05 is optional cleanup; E06 exists already; E01 needs workload evidence rather than another favorable synthetic test. This is a stopping decision for the current investigation, not a claim that the page cannot be made faster.

## Articles and reviews

- [E01 experiment article](iteration-01/e01-experiment-article.md)
- [E02 experiment article](iteration-01/e02-experiment-article.md)
- [E03 experiment article](iteration-01/e03-experiment-article.md)
- [E04 experiment article](iteration-01/e04-experiment-article.md)
- [E05 experiment article](iteration-01/e05-experiment-article.md)
- [E06 experiment article](iteration-01/e06-experiment-article.md)
- [E03 confirmation A1](iteration-02/e03-a1-article.md)
- [E03 confirmation B1](iteration-02/e03-b1-article.md)
- [E03 confirmation A2](iteration-02/e03-a2-article.md)
- [E03 confirmation B2](iteration-02/e03-b2-article.md)
- [First Sol review](iteration-01/sol-recommendations.md)
- [Final Sol review](iteration-02/sol-final-review.md)
- [Initial plan](iteration-01/plan.md) and [confirmation plan](iteration-02/plan.md)

The accompanying ZIP contains this report, all checkpoint articles, raw outputs, candidate patches, and preserved evidence. `SHA256SUMS.txt` identifies every bundled file.
