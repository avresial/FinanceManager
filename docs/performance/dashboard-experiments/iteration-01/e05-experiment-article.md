# E05 — Hoist `GetStoredBondsIds()` out of the daily net-worth loop

Experiment article for iteration 01, experiment E05. The candidate patch and all measurements were
reviewed by the parent agent before this restoration pass; this document records the closed-out state:
verification, restoration, post-restoration test proof, measurement comparison, and conclusion.

## Hypothesis

`NetWorthService.GetNetWorth(userId, currency, start, end)` rebuilds each bond account's stored-bond-id
list (`GetStoredBondsIds()`, a Select/Concat/Distinct chain) on **every day of the range**, inside the daily
loop. Accounts are not mutated inside the loop, so resolving each account's ids once before the loop should
remove redundant per-day list construction at zero behavioral cost — reducing CPU time (and allocations) for
`GET api/Dashboard/overview`, whose net-worth series dominates cold response time on this seed.

## The change (exact, local tuple array — no cache)

Two-file scope only; nothing else touched:

1. `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs` (range overload only):
   ```csharp
   // Accounts are not mutated inside the daily loop, so resolve each account's stored bond ids once;
   // calling GetStoredBondsIds() per day would rebuild its Select/Concat/Distinct list every iteration.
   var bondAccountsWithIds = bondAccounts.Select(account => (Account: account, DetailsIds: account.GetStoredBondsIds())).ToArray();

   ...
   foreach (var (account, detailsIds) in bondAccountsWithIds)
   {
       foreach (var detailsId in detailsIds)
       ```
2. `code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs`: one appended regression test,
   `GetNetWorth_OverRange_BondAccounts_ValueEachInstrumentOncePerAccount`, plus a private
   `CreateZeroRateDetails(int id)` fixture helper.

No cache, no new abstraction: the tuple array is a request-local variable living inside the method. The point
overload, `BondDashboardContext`, repository calls, descending date order, rounding, and
`GetStoredBondsIds()` itself (ordering: entries' ids first, then boundary keys, distinct) are untouched, so
boundary-key handling and dedup semantics are preserved by construction — verified behaviorally by the new test.

### Per-account dedup / boundary parity test

The regression test pins the exact selection/dedup contract on a hand-computable zero-rate fixture
(range 2024-01-01..2024-01-03, `UnitValue = 100m`, price = Value × UnitValue):

| Day | Account A (dedup case: instrument in-range + boundary) | Boundary-only instrument 2 | Account B (shared instrument, priced independently) | Total |
|---|---|---|---|---|
| Jan 1 | day entry (10) → 1000 | 4 × 100 = 400 | 7 × 100 = 700 | **2100** |
| Jan 2 | next-older carry-over (10) → 1000 | 400 | 700 | **2100** |
| Jan 3 | day entry (15) → 1500 | 400 | 700 | **2600** |

It asserts: no duplicate counting when an instrument appears both in-range and at the boundary in one account;
a boundary-only instrument contributes exactly once per account; a shared instrument is priced independently per
account (counted twice on Jan 3); empty-account path; result has exactly 3 keys in descending date order. All
expected values are computed independently from the fixture — no self-comparison, no timing assertions.

Test evidence:

- Candidate validation run (with the new test present): **total 2254, failed 0, succeeded 2254** →
  [`raw/e05-candidate-tests.log`](./raw/e05-candidate-tests.log), exit `0`
  ([details](./e05-implementation.md)). This is the "2254 pass" proof that the dedup/boundary parity test passes.
- Post-restoration run (candidate removed; suite back to 2253): **total 2253, failed 0, succeeded 2253** →
  [`raw/e05-restoration-tests.log`](./raw/e05-restoration-tests.log), exit `0` in
  [`raw/e05-restoration-tests.exit`](./raw/e05-restoration-tests.exit).

## Before/after measurements (single cold pair, N = 10 each)

Exact values from the full JSON reports; delta = (candidate − baseline)/baseline. Job:
InProcessEmitToolchain, IterationCount=10, WarmupCount=3, `FM_BENCH_COLD_CACHE=1`, `FM_BENCH_RESEED=0`, seed months 6.

### GET api/Dashboard/overview (6 months) — N = 10

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Mean | 81,443,704.15 ns (81.44 ms) | 81,228,638.60 ns (81.23 ms) | −0.264067% |
| Error (99.9% CI half-width) | 3,338,207.252762312 ns (3.34 ms) | 10,236,208.697495745 ns (10.24 ms) | +206.637902% |
| StdDev | 2,208,017.4095605547 ns (2.21 ms) | 6,770,618.2692111805 ns (6.77 ms) | +206.637902% |
| Median | 81,190,005.125 ns (81.19 ms) | 80,314,776.125 ns (80.31 ms) | −1.078001% |
| P95 | 84,689,653.05 ns (84.69 ms) | 89,228,008.3625 ns (89.23 ms) | +5.358807% |
| Allocated / op | 5,588,214 B (5.33 MB) | 5,543,768 B (5.29 MB) | −0.795353% |

### GET api/Dashboard/overview (1 month) — N = 10

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Mean | 55,619,925 ns (55.62 ms) | 54,992,782.30 ns (54.99 ms) | −1.127550% |
| Error (99.9% CI half-width) | 2,499,973.2591809477 ns (2.50 ms) | 2,540,302.978738412 ns (2.54 ms) | +1.613206% |
| StdDev | 1,653,577.5228274628 ns (1.65 ms) | 1,680,253.135263416 ns (1.68 ms) | +1.613206% |
| Median | 55,241,369.75 ns (55.24 ms) | 54,503,458.375 ns (54.50 ms) | −1.335795% |
| P95 | 58,135,774.0125 ns (58.14 ms) | 57,034,428.6375 ns (57.03 ms) | −1.894437% |
| Allocated / op | 3,030,266 B (2.89 MB) | 3,014,070 B (2.87 MB) | −0.534475% |

Confidence intervals overlap on both benchmarks; the candidate's 6-month run shows ~3× baseline variance
(StdDev 2.21 ms → 6.77 ms). Mean deltas are inside run-to-run noise for a single N=10 cold pair — no significance
claim is made. The only consistently-signed signal across both windows is the small allocation decrease
(−44,446 B/op at 6 months, −16,196 B/op at 1 month), observed once, not re-confirmed.

## Reproduction commands

Benchmark (run from `code/FinanceManager.Benchmarks`):

```sh
# Baseline tree: candidate two files absent (restored state); Candidate: e05-candidate.patch applied
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 \
  dotnet run -c Release -- --filter '*Dashboard*' \
  --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-baseline-cold \
  > ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-baseline-cold.log 2>&1

LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 \
  dotnet run -c Release -- --filter '*Dashboard*' \
  --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-candidate-cold \
  > ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-candidate-cold.log 2>&1
```

Unit tests (run from `code/`):

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj \
  > ../docs/performance/dashboard-experiments/iteration-01/raw/e05-restoration-tests.log 2>&1; \
echo $? > ../docs/performance/dashboard-experiments/iteration-01/raw/e05-restoration-tests.exit
```

## Environment and hashes

- BenchmarkDotNet v0.15.8; macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 10 cores
- .NET SDK 10.0.302; runtime .NET 10.0.10 Arm64 RyuJIT armv8.0-a; GC Non-concurrent Workstation
- `LANG=en_US.UTF-8`, `LC_ALL=en_US.UTF-8` for all runs (benchmarks and tests)
- HEAD before verification, benchmarking, and restoration: `8f987e2e5f909716ece1d396c2701fbc3707a`
  ("Add dashboard valuation benchmark coverage")
- Seed template SHA256 (`code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`),
  before and after both benchmarks: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`

## Raw artifacts, reports, and patches

- Candidate patch (exact two-file diff): [`e05-candidate.patch`](./e05-candidate.patch)
- Pre-restoration full worktree snapshot:
  [`evidence/e05-pre-restoration-worktree.patch`](./evidence/e05-pre-restoration-worktree.patch) (149 lines; all four
  then-modified files, including the preserved unrelated changes)
- Implementation + numeric fixture notes: [`e05-implementation.md`](./e05-implementation.md)
- Baseline measurement write-up: [`e05-baseline.md`](./e05-baseline.md); candidate cold write-up: [`e05-candidate-cold.md`](./e05-candidate-cold.md)
- Baseline raw: [`raw/e05-baseline-cold.log`](./raw/e05-baseline-cold.log), run directory
  `raw/e05-baseline-cold/` with [full JSON](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json),
  [GitHub markdown](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md),
  [CSV](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv),
  [HTML](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
- Candidate raw: [`raw/e05-candidate-cold.log`](./raw/e05-candidate-cold.log),
  [`raw/e05-candidate-cold.exit`](./raw/e05-candidate-cold.exit) (`0`), run directory `raw/e05-candidate-cold/` with
  [full JSON](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json),
  [GitHub markdown](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md),
  [CSV](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv),
  [HTML](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html), and run log
  [`FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-135922.log`](./raw/e05-candidate-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-135922.log)
- Candidate unit-test log (2254 pass): [`raw/e05-candidate-tests.log`](./raw/e05-candidate-tests.log)
- Restoration unit-test log (2253 pass, exit `0`): [`raw/e05-restoration-tests.log`](./raw/e05-restoration-tests.log),
  [`raw/e05-restoration-tests.exit`](./raw/e05-restoration-tests.exit)

## Restoration evidence (E05 closed out on this worktree only)

- **Pre-restoration verification:** `git rev-parse HEAD` = `8f987e2e5f909716ece1d396c2701fbc3707a`; the exact diff of
  `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs` +
  `code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs` was compared against
  [`e05-candidate.patch`](./e05-candidate.patch) — **byte-identical** (also identical when diffed against HEAD).
  Full pre-restoration worktree state saved to
  [`evidence/e05-pre-restoration-worktree.patch`](./evidence/e05-pre-restoration-worktree.patch). No mismatch → proceed.
- **Restoration:** `git restore --source=HEAD -- <the two exact paths>` (exit 0) at **14:04:35**, within the
  5-minute window of task start (~14:02). Note on mechanics: this environment cannot resolve the raw SHA as an object
  (`git cat-file -t 8f987e2…` → "Not a valid object name"; only HEAD's commit object is unavailable locally, its tree
  and all blobs resolve), while `git rev-parse HEAD^{commit}` returns exactly that SHA — so `--source=HEAD` is the
  provably identical source for this restore.
- **Post-restoration proof:** candidate two-path diff vs HEAD is **empty**; unrelated changes preserved untouched —
  `git status --porcelain` after restoration shows only
  `M code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs`,
  `M code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs`, and untracked `docs/performance/`.
- **Post-restoration tests:** exit **0**, total **2253**, failed **0**, succeeded **2253** (suite without the candidate's
  added test), duration 22s 735ms — [`raw/e05-restoration-tests.log`](./raw/e05-restoration-tests.log).

## Conclusion

- **No demonstrated latency gain on this seed.** Mean deltas of −0.26% (6 months) and −1.13% (1 month) are inside
  run-to-run noise for a single N=10 cold pair; the candidate's 6-month variance (~3× baseline) makes the mean delta
  non-interpretable. **A small allocation decrease was observed once** (−44,446 B/op at 6 months, −16,196 B/op at
  1 month) but is unconfirmed in a second run.
- This change is a **low-complexity cleanup candidate for final Sol review — not a proven page-speed recommendation**.
- Single pair / within-run noise: both benchmarks completed (2 executed per side), but the **baseline process exit was
  never directly captured** (`e05-baseline-cold.exit` does not exist; completion evidenced by artifacts/log only).
- No confirm run and no scaled fixture were done: behavior at larger ranges/seed sizes is **explicitly unmeasured — no
  scaling claim is made**.
- Warm runs omitted by code path: a warm Dashboard cache hit refreshes assets only, not the net-worth series, so a
  warm measurement would not exercise this change.
- All timings are in-process API measurements over SQLite inside the benchmark host (BenchmarkDotNet
  InProcessEmitToolchain) — **not browser/page metrics**.

**STOP.** E06 not started. Candidate change removed from this worktree; all evidence, logs, patches, and the two
unrelated preexisting modifications preserved as listed above.
