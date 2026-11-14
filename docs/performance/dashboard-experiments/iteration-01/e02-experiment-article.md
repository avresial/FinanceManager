# E02 Experiment Article — NetWorth range fetch with `includeEntryMetadata: false`

Status: **complete, candidate removed from production tree (reverted to HEAD).** This article is a
permanent record of the experiment; keep it alongside the raw artifacts in `raw/`.

## Hypothesis and change

The dashboard's net-worth-over-range endpoint (`GET api/Dashboard/overview`) calls
`NetWorthService.GetNetWorth(userId, currency, start, end)`, which loads all `CurrencyAccount` and
`BondAccount` rows for the range. The net-worth calculation only consumes entry dates and values; account labels are
never used on this path.

**Hypothesis:** passing `includeEntryMetadata: false` to both repository calls omits label collection loading, reducing the data materialized enough to reduce server-side time and allocations.

**Two-call change (the exact candidate):** in
`code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs`, the two range fetches become:

```csharp
await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end, includeEntryMetadata: false))
    currencyAccounts.Add(account);
...
await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end, includeEntryMetadata: false))
    bondAccounts.Add(account);
```

The repository cache key includes this flag, so the effect was explicitly "must be measured" — it could
also *hurt* by creating a separate cache bucket. The candidate patch also extends
`code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs`: mock setups updated for the
new overload and one candidate-only routing test verifying both calls resolve with `includeEntryMetadata: false`.

## Exact source patch (preserved)

- Patch file: [`e02-candidate.patch`](./e02-candidate.patch) — i.e.
  `docs/performance/dashboard-experiments/iteration-01/e02-candidate.patch`
- SHA-256: `13aacb77ab44c464a039e8c8df30a376ba979f708d3f4fdf6b567e9aaffda0dd`
- Verified byte-identical to the live `git diff` of exactly those two files both before and during this
  confirmation run (re-checked in step 1 below).

## Harness state (retained across paired runs, identical)

The worktree carried, besides the candidate, harness/preexisting diffs that were **left untouched and
identical for every measurement** (recorded here because provenance is uncertain):

- `code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs` — includes both the `seedMonths`
  wiring (`FM_BENCH_SEED_MONTHS`) and a `CreateHostScope` accessor. Retained harness diff; recorded as-is,
  not modified by this experiment.
- `code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs` — preexisting modification.

Full worktree state (all four modified files) captured at start of confirmation:
[`evidence/e02-confirmation-worktree.patch`](./evidence/e02-confirmation-worktree.patch).

## Measurement protocol

Benchmark host: `code/FinanceManager.Benchmarks`, Release, in-process BenchmarkDotNet against a local
SQLite seed database. Command (identical for both confirmation runs; 6-month seed dataset):

```bash
cd code/FinanceManager.Benchmarks
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 \
  dotnet run -c Release -- --filter '*Dashboard*' \
  --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/<run-name> \
  > ../../docs/performance/dashboard-experiments/iteration-01/raw/<run-name>.log 2>&1
```

Cold-cache mode resets the repository cache between cases; `FM_BENCH_RESEED=0` means no reseeding — both
confirmation runs consumed the same seed template. Seed integrity verified before and after:

| Check | SHA-256 of `code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite` |
|---|---|
| Before candidate run (step 1) | `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` ✓ expected |
| After baseline run (step 4) | `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` ✓ unchanged |

Both confirmation runs exited 0 with exactly two completed cases and no errors/exceptions in the logs.
Results inspected as JSON (`report-full.json`) and markdown (`report-github.md`).

## Measurements — initial paired run (baseline first, candidate second)

From [`raw/e02-baseline-warm`](./raw/e02-baseline-warm), [`raw/e02-baseline-cold`](./raw/e02-baseline-cold),
[`raw/e02-candidate-warm`](./raw/e02-candidate-warm), [`raw/e02-candidate-cold`](./raw/e02-candidate-cold):

| Case | 6 months mean (±99.9% CI half) | 1 month mean (±99.9% CI half) | Allocated (6m / 1m) |
|---|---|---|---|
| Warm baseline | 15.98 ms ± 0.546 | 15.08 ms ± 1.203 | 645.16 KB / 618.63 KB |
| Warm candidate | 17.99 ms ± 3.123 | 14.57 ms ± 1.204 | 644.25 KB / 614.21 KB |
| Cold baseline | 85.32 ms ± 8.006 (sd 5.295) | 54.93 ms ± 1.765 (sd 1.050) | 5.31 MB / 2.89 MB |
| Cold candidate | 100.23 ms ± 4.349 (sd 2.877) | 67.35 ms ± 3.786 (sd 2.504) | 5.93 MB / 3.27 MB |

Initial cold deltas (candidate − baseline): **+14.91 ms (+17.5%)** on 6 months, **+12.42 ms (+22.6%)**
on 1 month; allocations higher by ~0.62 MB / ~0.38 MB.

## Measurements — reverse-order cold confirmation (candidate first, baseline second)

This run repeated the experiment in the opposite order to rule out order/run-to-run drift: candidate was
measured first with the patch applied, then only the two candidate files were restored to HEAD and the
baseline measured immediately after on the same seed/config/harness.

From [`raw/e02-confirm-candidate`](./raw/e02-confirm-candidate) (log
[`e02-confirm-candidate.log`](./raw/e02-confirm-candidate.log)) and
[`raw/e02-confirm-baseline`](./raw/e02-confirm-baseline) (log
[`e02-confirm-baseline.log`](./raw/e02-confirm-baseline.log)):

| Case | 6 months mean (±99.9% CI half, sd) | median / P95 | 1 month mean (±99.9% CI half, sd) | median / P95 | Gen0 per 1k ops | Allocated (6m / 1m) |
|---|---|---|---|---|---|---|
| Cold candidate (first) | 103.26 ms ± 5.847, sd 3.058 | 104.22 / 105.85 | 66.65 ms ± 3.439, sd 2.275 | 66.55 / 69.50 | 500 (both) | 5.86 MB / 3.27 MB |
| Cold baseline (second) | 93.22 ms ± 9.272, sd 6.133 | 91.59 / 102.64 | 59.97 ms ± 2.157, sd 1.128 | 59.94 / 61.37 | 750 (6m) / 250 (1m) | 5.36 MB / 2.89 MB |

Confirmation cold deltas (candidate − baseline): **+10.04 ms (+10.8%)** on 6 months,
**+6.68 ms (+11.1%)** on 1 month; allocations higher by ~0.50 MB / ~0.38 MB.

The candidate is slower in **both orderings and both windows**; warm-cache runs showed no clear
difference (candidate deltas within the wide uncertainty bands). The cold-run error bars are large, so
these deltas cannot be called statistically significant — but the direction is consistent across four
independent cold measurements.

## Correctness evidence

- With candidate applied: `dotnet test` on `FinanceManager.Tests.Unit` → **2254/2254 passed** (baseline
  2253 + 1 candidate-only routing test), log [`raw/e02-candidate-tests.log`](./raw/e02-candidate-tests.log).
- Both benchmark runs with the candidate in place completed without errors.

## Restoration evidence

- Restored exactly two paths to HEAD `8f987e2e` via
  `git restore --source=8f987e2e -- code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs`
  (authorized experimental rollback; no broad restore/clean/reset).
- Verified: `git diff` of those two files is now **empty**; the exact candidate patch remains preserved in
  `e02-candidate.patch`. The prior committed routing tests are untouched.
- Remaining worktree diffs after restoration: only the retained harness/preexisting files listed above
  (`BenchmarkEnvironment.cs`, `GuestAccountSeeder.cs`), unchanged from before this run.
- Post-restoration unit tests: **2253/2253 passed**, log
  [`raw/e02-restoration-tests.log`](./raw/e02-restoration-tests.log).

## Mechanism inference (inference, not proof)

The repository's in-memory cache key includes the `includeEntryMetadata` flag. The candidate therefore
introduces a **separate metadata-free cache bucket** from the one used by other dashboard/account paths:
on cold runs every first access to each variant is its own SQLite load of the account rows, i.e. repeated
account loads rather than reuse of an already-populated entry — while also adding per-row handling for the
flag itself. This is consistent with candidate being slower and allocating more in cold mode yet roughly
equal once warm (cached). No SQL query-count instrumentation was run, so this is an inference from the
cache-key design plus timing behavior, **not a measured proof of extra queries**. The slightly higher
allocations despite smaller rows are not explained by the patch alone; treat as consistent-but-unproven.

## Limitations

- Local SQLite seed database, in-process endpoint timing on a single machine; short runs (10 iterations),
  high variance in cold mode — no statistical significance can be claimed from these deltas.
- No browser/client-side measurement; server-side timing only. Not representative of production databases
  or network latency.
- No query-count instrumentation; mechanism is inferred, not counted.

## Recommendation

**Do not adopt the candidate.** Repeated evidence in both run orders shows a consistent cold-mode
regression (+10–23% mean time per case) with higher allocations and no warm-cache benefit, while costing an
extra cache bucket for a payload field this endpoint never used. The change has been removed from the
production tree (both files restored to HEAD); keep `e02-candidate.patch` archived in case a future design
(unified cache key or label-free query variant) wants to re-test it under instrumentation.

## Reproduction

```bash
# 1. Apply candidate exactly:
git apply docs/performance/dashboard-experiments/iteration-01/e02-candidate.patch

# 2. Cold run (candidate, first of pair):
cd code/FinanceManager.Benchmarks && LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 \
  FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 \
  dotnet run -c Release -- --filter '*Dashboard*' \
  --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-confirm-candidate

# 3. Restore exactly two files:
git restore --source=8f987e2e -- \
  code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs \
  code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs

# 4. Cold run (baseline, second of pair): same command with artifacts e02-confirm-baseline
```

Raw artifacts: `raw/e02-confirm-candidate{,.log}`, `raw/e02-confirm-baseline{,.log}`; initial pair in
`raw/e02-{baseline,candidate}-{warm,cold}{,.log}`. Worktree state snapshot:
`evidence/e02-confirmation-worktree.patch`.
