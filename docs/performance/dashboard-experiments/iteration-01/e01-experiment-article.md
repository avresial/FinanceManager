# E01: Reuse bond price history across dates — experiment article

Status: **complete — no demonstrated page-level speedup; candidate restored.** All raw evidence, patch and articles are retained. This is an experiment article for the Sol review, not a measured recommendation to ship.

## Hypothesis

`BondAccountEntry.GetPriceAt(date)` rebuilds the entire price series up to `date` on every call. The dashboard net-worth range loop calls it once per selected bond snapshot **per day**, so ascending dates repeatedly recompute overlapping prefixes. Computing each unchanged entry's bounded history once per request (fixed horizon = request end) and reusing date lookups should reduce repeated work in the overview endpoint, especially for long-lived bonds and long windows — while preserving gaps, posting dates, rate changes, rounding and capitalization exactly.

## Exact change (candidate; patch: [e01-candidate.patch](./e01-candidate.patch))

Three files, request-local only, no shared/static cache, no freshness-semantic change:

1. `code/FinanceManager.Application/FinancialAccounts/Bond/Valuation/BondDashboardContext.cs`
   - New request-local `_histories`: key `(AccountId, EntryId, BondDetailsId, PostingDate, Value)` → `(Through, Dictionary<DateOnly, decimal> Prices)`.
   - New public `PreparePriceHistory(accountId, entry, through)`: no-op if an existing history already covers `through`; skips preparation when `through` precedes the posting date (pre-posting behavior preserved via fallback); otherwise stores `entry.GetPrice(through, details)` once. Missing definitions still throw where pricing actually needs them.
   - `GetOrComputePrice`: exact-date `_prices` cache lookup retained first; then, before the unchanged `GetPriceAt` fallback, a prepared history covering `date <= Through` is served via `Prices.GetValueOrDefault(date)` (zero for absent/gap and pre-posting dates — exactly matching `GetPriceAt`). History never auto-extends.
2. `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs` (range overload only)
   - `bondHistoryEnd = DateOnly.FromDateTime(end)` computed once before the daily loop; `PreparePriceHistory(..., bondHistoryEnd)` called per selected entry immediately before the existing `GetOrComputePrice`. Point overload unchanged.
3. `code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs` — prepared-path parity tests (ascending/descending/shuffled theory through day 730, expiry-gap fact through day 10, entry-mutation staleness bypass, fallback-beyond-horizon then explicit extension).

Full edit description and invariant analysis: [e01-implementation-result.md](./e01-implementation-result.md).

## Correctness evidence

- Unit tests after candidate implementation: **2,255 passed / 0 failed**, exit 0 (`raw/e01-implementation-tests.log`), explicit English locale.
- Endpoint validation before both candidate benchmark runs: all 48 benchmarked API calls succeeded with non-empty responses (`raw/e01-candidate-validate.log`).
- Prepared-path tests assert exact parity with `entry.GetPriceAt` for every accessed date, including gaps and pre-posting dates; a mutated entry bypasses its stale history (key includes `Value`) and matches fresh computation.

## Measurement setup

- Baseline source: `f5178edd`; test-only commit on top: `f3945df5` (parity tests). Candidate measured uncommitted at HEAD `f3945df5`.
- Pre-existing uncommitted edits (`GuestAccountSeeder.cs`, `BenchmarkEnvironment.cs` seed-window scaling) preserved untouched in every run; identical dataset for baseline and candidate — seed template SHA-256 `c2f4978d…b0e37867` verified unchanged.
- Environment (identical for all runs): BenchmarkDotNet 0.15.8, macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302 / runtime 10.0.10 Arm64; `FM_BENCH_SEED_MONTHS=6`, `FM_BENCH_RESEED=0`; IterationCount=10, IterationTime=250ms.
- Server-side API timings for `GET api/Dashboard/overview` (1-month and 6-month windows), warm cache (`FM_BENCH_COLD_CACHE=0`) and cold/cache-bypassed (`FM_BENCH_COLD_CACHE=1`). Not browser paint, not end-to-end latency.

## Before / after — warm cache ([e01-candidate-warm.md](./e01-candidate-warm.md))

| Case | Metric | Baseline | Candidate | Δ |
|---|---|---:|---:|---:|
| 6 months | Mean | 15.732 ms | 16.351 ms | +0.618 ms (+3.93%) — inside run CIs (±0.355 / ±0.911) |
| 6 months | Median | 15.759 ms | 16.303 ms | +0.544 ms (+3.45%) |
| 6 months | P95 | 16.070 ms | 17.151 ms | +1.081 ms (+6.72%) |
| 6 months | Allocated/op | 660,948 B | 660,326 B | −622 B (−0.09%) |
| 1 month | Mean | 13.838 ms | 15.029 ms | +1.191 ms (+8.61%) — inside run CIs (±1.176 / ±1.876) |
| 1 month | Median | 13.660 ms | 15.141 ms | +1.481 ms (+10.84%) |
| 1 month | P95 | 15.117 ms | 16.741 ms | +1.624 ms (+10.74%) |
| 1 month | Allocated/op | 630,074 B | 630,162 B | +88 B (+0.014%) |

All mean/median/P95 deltas sit inside the within-run 99.9% CI half-widths; per-run CIs overlap in both windows. Direction is mildly (weakly) negative but not evidence of regression with one run per configuration and larger candidate-side variance. Allocations unchanged (−0.09% / +0.014%).

## Before / after — cold cache ([e01-candidate-cold.md](./e01-candidate-cold.md))

| Case | Metric | Baseline | Candidate | Δ |
|---|---|---:|---:|---:|
| 6 months | Mean | 88.795 ms | 88.881 ms | +0.086 ms (+0.10%) — inside run CIs (±~6.4 / ±6.9) |
| 6 months | Median | 89.542 ms | 90.283 ms | +0.741 ms (+0.83%) |
| 6 months | P95 | 93.774 ms | 93.510 ms | −0.264 ms (−0.28%) |
| 6 months | Allocated/op | 5,571,170 B | 5,220,968 B | **−350,202 B (−6.29%)** |
| 1 month | Mean | 55.740 ms | 56.217 ms | +0.477 ms (+0.86%) — inside run CIs (±~2.5 / ±3.2) |
| 1 month | Median | 55.660 ms | 55.929 ms | +0.269 ms (+0.48%) |
| 1 month | P95 | 57.939 ms | 59.168 ms | +1.229 ms (+2.12%) |
| 1 month | Allocated/op | 3,031,198 B | 2,954,636 B | **−76,562 B (−2.53%)** |

No proven speedup: both mean deltas far inside run CIs; mixed sub-metric signals consistent with noise. Allocations decreased in both windows — deterministic and the most credible signal here, but from a single run per configuration: promising, not established.

## Targeted mechanism microbenchmark ([e01-mechanism.md](./e01-mechanism.md))

The dashboard dataset did not surface the hypothesis's intended workload (long-lived bond, long window), so a focused domain benchmark was added (`code/FinanceManager.Benchmarks/Benchmarks/BondHistoryBenchmarks.cs`; compiles with or without the candidate code; pure domain — no host/request context). Fixture: 3,650-day-old bond, one calculation method covering the whole window; windows of 31 and 183 days, ascending and descending. `GlobalSetup` asserts per-date parity between point pricing and prepared history before any timing.

| Window | Order | RepeatedPointPricing mean | PreparedHistory mean (incl. full preparation) | Paired ratio |
|---:|---|---:|---:|---:|
| 31 days | ascending | 14.53 ms / 15,436 KB | 0.469 ms / 498.9 KB | 0.03 |
| 31 days | descending | 14.80 ms / 15,437 KB | 0.479 ms / 498.9 KB | 0.03 |
| 183 days | ascending | 85.64 ms / 90,257 KB | 0.480 ms / 498.9 KB | 0.006 |
| 183 days | descending | 84.41 ms / 90,257 KB | 0.477 ms / 498.9 KB | 0.006 |

The mechanism itself is real and large: repeated point pricing grows with the window (~linear per-date rebuild of an ever-longer prefix) while one prepared history plus dictionary lookups is flat — ~31× at 31 days, ~178× at 183 days, with ~96% less allocation. But this endpoint-visible benefit **did not appear** in the warm/cold dashboard measurements above: on the current seed data and windows, bond price computation is a small fraction of overview cost, so page-level timings are statistically unchanged.

## Conclusions

- The candidate is semantically correct (2,255 tests; per-date parity with `GetPriceAt` including gaps/expiry/pre-posting) and reduces cold-cache allocations (−6.3% / −2.5%, single run).
- It does **not** demonstrate a page-level speedup on the measured dashboard workloads: all timing deltas are inside noise, mildly negative in direction for warm runs.
- The mechanism win is real but workload-bound: it pays off only when long-lived bond snapshots are priced across long ascending windows — not exercised by the current demo seed/windows at a level visible in endpoint timings.
- Recommendation for review: **do not implement E01 as-is**. If long-window/long-lived-bond overview latency becomes a target, revisit with a benchmark fixture that actually exercises it (scaled `FM_BENCH_SEED_MONTHS` + long-held bond), and consider pairing with E05 (bond-id precompute) since both touch the same loop. The retained patch is small, request-local, and free of freshness-semantic risk if re-measured favorably.

## Baseline restoration

After measurement, all three E01-owned files (`BondDashboardContext.cs`, `NetWorthService.cs`, `BondDashboardContextTests.cs`) were restored to the `f3945df5` baseline via `git checkout --`; the candidate remains available only as [e01-candidate.patch](./e01-candidate.patch). Verification, explicit English locale from `code/`:

- Unit tests: **2,249 passed / 0 failed**, exit 0 (`raw/e01-restoration-tests.log`) — exactly the pre-candidate verified count.
- Benchmark project build (Release): 0 warnings / 0 errors with the retained `BondHistoryBenchmarks.cs` in place (`raw/e01-restoration-benchmarks-build.log`).

Two non-E01 working-tree items were handled during restoration and parked under [evidence/](./evidence/) rather than deleted:

- `AccountRepositoryTests.cs` (tracked at HEAD) was found mid-write with a syntactically broken E02 WIP rewrite (~issue #458): missing parentheses on bare `Mock.Of<...>` calls, CS9175 collection expressions inside Moq expression trees, CS9176 untyped list initializers, and a semantic bug comparing account fields instead of entry fields. It was never part of any verified passing run in that state; the HEAD version (one test) is restored so the baseline matches `f3945df5` exactly. The WIP copy (after two minimal paren fixes to make it reviewable) is parked at [evidence/account-repository-tests-e02-wip.cs](./evidence/account-repository-tests-e02-wip.cs).
- `BondPriceReuseBenchmarks.cs` (untracked, never measured — no raw artifacts exist for it) targets the candidate-only API `PreparePriceHistory` and does not compile against the restored baseline (7 CS errors). Parked at [evidence/bond-price-reuse-benchmarks-e01-candidate-only.cs](./evidence/bond-price-reuse-benchmarks-e01-candidate-only.cs) with a park note; to run it, apply `e01-candidate.patch` first. The companion `BondHistoryBenchmarks.cs` compiles against both baseline and candidate and remains in place as the retained benchmark test.

## Limitations

- One run per configuration; no repeated paired runs to estimate run-to-run variance beyond within-run CIs.
- Server-side API timings only — not browser paint or end-to-end latency.
- Microbenchmark isolates the domain computation; it does not measure request-context dictionary overhead, API or page performance (stated in the benchmark's XML comment).
- Pre-existing uncommitted seed-window edits were present in every run (identical baseline and candidate), so A/B comparisons remain valid; they are not part of E01.

## Reproduction

From repository root:

```sh
# Unit tests (candidate restored at e01-candidate.patch):
git apply docs/performance/dashboard-experiments/iteration-01/e01-candidate.patch
cd code && LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj

# Warm / cold dashboard A/B (from code/FinanceManager.Benchmarks):
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=0 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 \
  dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/repro-e01-warm

LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 \
  dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/repro-e01-cold

# Mechanism microbenchmark:
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet run -c Release -- --filter '*BondHistoryBenchmarks*' \
  --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/repro-e01-mechanism
```

## Raw artifacts index

- Baselines: [e01-warm-baseline.md](./e01-warm-baseline.md), [e01-cold-baseline.md](./e01-cold-baseline.md) — `raw/e01-baseline-{warm,cold}{,.log}`
- Candidate runs: [e01-candidate-warm.md](./e01-candidate-warm.md), [e01-candidate-cold.md](./e01-candidate-cold.md) — `raw/e01-candidate-{validate,warm,cold}{,.log}`
- Implementation/tests: [e01-implementation-result.md](./e01-implementation-result.md) — `raw/e01-implementation-tests.log`, patch [e01-candidate.patch](./e01-candidate.patch)
- Correctness checkpoints: [e01-checkpoint-01.md](./e01-checkpoint-01.md)/[result](./e01-checkpoint-01-result.md), [e01-checkpoint-02.md](./e01-checkpoint-02.md)/[result](./e01-checkpoint-02-result.md) — `raw/e01-checkpoint-{01,02}-tests.log`, `raw/e01-parent-unit-tests.log`
- Mechanism: [e01-mechanism-setup.md](./e01-mechanism-setup.md), [e01-mechanism.md](./e01-mechanism.md) — `raw/e01-mechanism{,-build,-tests,.log}`, `raw/e01-mechanism/results/`
- Restoration: `raw/e01-restoration-tests.log`, `raw/e01-restoration-benchmarks-build.log`; evidence: [evidence/account-repository-tests-e02-wip.cs](./evidence/account-repository-tests-e02-wip.cs), [evidence/bond-price-reuse-benchmarks-e01-candidate-only.cs](./evidence/bond-price-reuse-benchmarks-e01-candidate-only.cs) (+ park note), `raw/preexisting-uncommitted-diff.patch`, `raw/preserved-diffs/`
