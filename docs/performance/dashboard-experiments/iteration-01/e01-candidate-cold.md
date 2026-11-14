# E01 candidate cold-cache run (e01-candidate-cold)

Cache-bypassed dashboard benchmark of the uncommitted E01 candidate (`FM_BENCH_COLD_CACHE=1`, `FM_BENCH_RESEED=0`, `FM_BENCH_SEED_MONTHS=6`), measuring API server work for both dashboard windows with the cache bypassed. This is **not** a first-process-startup or browser-latency measurement. Single measurement checkpoint; no source edits, no warm rerun.

Candidate diff under test (uncommitted in worktree at HEAD `f3945df5`):

- `code/FinanceManager.Application/FinancialAccounts/Bond/Valuation/BondDashboardContext.cs`
- `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs`
- `code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs`

Pre-existing uncommitted edits preserved untouched: `GuestAccountSeeder.cs`, `BenchmarkEnvironment.cs`.

## Commands and exits

Run from `code/FinanceManager.Benchmarks`:

1. Validation (first artifact):

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 dotnet run -c Release -- --validate > ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-candidate-validate.log 2>&1
```

Exit code: **0** — "All 48 benchmarked calls succeeded" (both dashboard calls `ok`).

2. Cold-cache benchmark:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-candidate-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-candidate-cold.log 2>&1
```

Exit code: **0** — global total time 00:00:12 (12.49 sec), executed benchmarks: 2; both completed, no failures.

## Results (candidate)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | Gen1/1000 ops |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 88.88 ms | 6.918 ms | 4.576 ms | 90.28 ms | 93.51 ms | 4.98 MB | 750 | 250 |
| GET api/Dashboard/overview (1 month) | 56.22 ms | 3.245 ms | 1.931 ms | 55.93 ms | 59.17 ms | 2.82 MB | 250 | 0 |

Cross-checked against `report-full.json` (ns):

- 6 months: mean 88,881,172.925 / median 90,283,078.125 / P95 93,509,970.35 / CI margin 6,917,768.20 / StdDev 4,575,675.34; bytes/op 5,220,968 (N=10, no outliers).
- 1 month: mean 56,217,252.277777776 / median 55,929,020.75 / P95 59,167,947.95 / CI margin 3,244,907.41 / StdDev 1,930,992.35; bytes/op 2,954,636 (N=9 — one outlier of 63.20 ms removed from statistics).

## Baseline comparison (vs e01-baseline-cold)

Baseline source: `raw/e01-baseline-cold/results/...report-full.json` (same host; baseline run 20:52, candidate run 21:01 on 2026-10-30). Percentages computed from full-JSON ns/byte precision.

| Case | Metric | Baseline | Candidate | Δ |
|---|---|---:|---:|---:|
| 6 months | Mean (ns) | 88,794,932.25 | 88,881,172.925 | +86,240.675 ns (+0.097%) |
| 6 months | Median (ns) | 89,541,750 | 90,283,078.125 | +741,328.125 ns (+0.828%) |
| 6 months | P95 (ns) | 93,774,129.65 | 93,509,970.35 | −264,159.30 ns (−0.282%) |
| 6 months | Allocated/op (B) | 5,571,170 | 5,220,968 | −350,202 B (−6.286%) |
| 1 month | Mean (ns) | 55,739,767.775 | 56,217,252.277777776 | +477,484.50 ns (+0.857%) |
| 1 month | Median (ns) | 55,660,375.125 | 55,929,020.75 | +268,645.625 ns (+0.483%) |
| 1 month | P95 (ns) | 57,938,630.34 | 59,167,947.95 | +1,229,317.61 ns (+2.122%) |
| 1 month | Allocated/op (B) | 3,031,198 | 2,954,636 | −76,562 B (−2.526%) |

## Interpretation / uncertainty

- **No proven speedup.** Both mean deltas are far inside the within-run 99.9% CI half-widths (6 months: +0.086 ms vs ±~6.4–6.9 ms; 1 month: +0.477 ms vs ±~2.5–3.2 ms). The candidate is statistically indistinguishable from baseline on time for both windows.
- Mixed sub-metric signals (6-month median higher, P95 lower; 1-month mean/median/P95 all slightly higher) are consistent with noise, not a trend.
- **Allocations decreased in both windows** (−6.286% / −2.526%). Per-operation allocation is deterministic and far less noisy than timing, so this direction is the more credible signal — but it comes from one run each; treat as promising, not established.
- One outlier removed from the 1-month candidate statistics (63.20 ms → N=9 vs baseline N=10); two values flagged as outliers in that benchmark (lower 53.07 ms, upper 60.01 ms). No outliers on the 6-month run.
- These are **API server-side timings** with the cache bypassed — not browser paint or end-to-end latency, and not first-process-startup cost.
- Single run per configuration; no repeated runs to estimate run-to-run variance beyond within-run CIs.

## Warnings / errors

No benchmark errors or exceptions (exit 0 plus "executed benchmarks: 2" verified in the log). Non-fatal warnings, same set as the baseline run:

- BenchmarkDotNet (×2): `Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — expected on macOS, does not affect measurements.
- ASP.NET Core `HttpsRedirectionMiddleware[3]`: `Failed to determine the https port for redirect.`
- EF Core `Query[20504]` (MultipleCollectionIncludeWarning): query loads related collections without configured `QuerySplittingBehavior`; defaults to `SingleQuery`.

## Reproducibility

- Git HEAD: `f3945df56fa0557e70de1859c70fc279faa30c54` (before and after run; candidate edits uncommitted, preserved).
- Seed template SHA-256 (`bin/Release/net10.0/benchmark-data/seed-template.sqlite`, from benchmark project cwd): `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` — verified before the run (matched expected) and unchanged after (`FM_BENCH_RESEED=0`).

## Environment (from report)

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83), Darwin 25.6.0
- Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- Toolchain=InProcessEmitToolchain, IterationCount=10, IterationTime=250ms, WarmupCount=3

## Raw artifacts

- Validation log: [raw/e01-candidate-validate.log](./raw/e01-candidate-validate.log)
- Cold run log: [raw/e01-candidate-cold.log](./raw/e01-candidate-cold.log)
- Per-run log: [raw/e01-candidate-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-210146.log](./raw/e01-candidate-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-210146.log)
- GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e01-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
- Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e01-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- CSV: [results/...DashboardBenchmarks-report.csv](./raw/e01-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
- HTML: [results/...DashboardBenchmarks-report.html](./raw/e01-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
