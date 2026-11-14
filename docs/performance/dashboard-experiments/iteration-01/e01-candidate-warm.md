# E01 candidate warm-cache run (e01-candidate-warm)

Warm-cache dashboard benchmark of the uncommitted E01 candidate (`FM_BENCH_COLD_CACHE=0`, `FM_BENCH_RESEED=0`, `FM_BENCH_SEED_MONTHS=6`), measuring API server work for both dashboard windows with the cache warm. This is **not** a browser-latency or end-to-end measurement — no browser timing claims are made here. Single measurement checkpoint; no source edits, no cold run.

Candidate diff under test (uncommitted in worktree at HEAD `f3945df5`):

- `code/FinanceManager.Application/FinancialAccounts/Bond/Valuation/BondDashboardContext.cs`
- `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs`
- `code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs`

Pre-existing uncommitted edits preserved untouched: `GuestAccountSeeder.cs`, `BenchmarkEnvironment.cs`. All source changes were left in place before and after the run.

## Commands and exits

Run from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=0 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-candidate-warm > ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-candidate-warm.log 2>&1
```

Exit code: **0** — global total time 00:00:11 (11.26 sec), executed benchmarks: 2; both completed, no failures.

## Results (candidate)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops |
|---|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 16.35 ms | 0.911 ms | 0.603 ms | 16.30 ms | 17.15 ms | 644.85 KB | 66.6667 |
| GET api/Dashboard/overview (1 month) | 15.03 ms | 1.876 ms | 1.241 ms | 15.14 ms | 16.74 ms | 615.39 KB | 66.6667 |

Cross-checked against `report-full.json` (ns):

- 6 months: mean 16,350,631.946666667 / median 16,302,988.866666667 / P95 17,150,502.07333333 / CI margin 910,976.5927813309 / StdDev 602,554.6121796035; bytes/op 660,326 (N=10, no outliers).
- 1 month: mean 15,029,081.386666665 / median 15,141,115.266666666 / P95 16,740,723.913333334 / CI margin 1,875,914.7973587392 / StdDev 1,240,801.4894799823; bytes/op 630,162 (N=10, no outliers).

## Baseline comparison (vs e01-baseline-warm)

Baseline source: `raw/e01-baseline-warm/results/...report-full.json` (same host; baseline run 20:50, candidate run 21:06 on 2026-10-30). Percentages computed from full-JSON ns/byte precision.

| Case | Metric | Baseline | Candidate | Δ |
|---|---|---:|---:|---:|
| 6 months | Mean (ns) | 15,732,403.923529413 | 16,350,631.946666667 | +618,228.023 ns (+3.9296%) |
| 6 months | Median (ns) | 15,758,678.911764707 | 16,302,988.866666667 | +544,309.955 ns (+3.4540%) |
| 6 months | P95 (ns) | 16,069,998.402941177 | 17,150,502.07333333 | +1,080,503.670 ns (+6.7237%) |
| 6 months | Allocated/op (B) | 660,948 | 660,326 | −622 B (−0.0941%) |
| 1 month | Mean (ns) | 13,838,024.758823529 | 15,029,081.386666665 | +1,191,056.628 ns (+8.6071%) |
| 1 month | Median (ns) | 13,659,988.970588235 | 15,141,115.266666666 | +1,481,126.296 ns (+10.8428%) |
| 1 month | P95 (ns) | 15,117,209.182352941 | 16,740,723.913333334 | +1,623,514.731 ns (+10.7395%) |
| 1 month | Allocated/op (B) | 630,074 | 630,162 | +88 B (+0.0140%) |

## Interpretation / uncertainty

- **No statistically significant timing change.** Both mean deltas sit inside the within-run 99.9% CI half-widths: 6 months +0.618 ms vs candidate ±0.911 ms (baseline ±0.355 ms); 1 month +1.191 ms vs candidate ±1.876 ms (baseline ±1.176 ms). The per-run CIs overlap in both windows (6 months: candidate [15.440, 17.262] ms vs baseline [15.377, 16.088] ms; 1 month: candidate [13.153, 16.905] ms vs baseline [12.662, 15.014] ms), so the differences are not distinguishable from noise at this confidence level.
- **Direction is consistent but weak.** All timing sub-metrics (mean/median/P95) are higher on the candidate in both windows — a mild hint of possible slowdown, but with one run per configuration and larger within-run variance on this candidate run (StdDev 0.603 ms vs 0.235 ms for 6 months; 1.241 ms vs 0.778 ms for 1 month), it is not evidence of a regression.
- **Allocations are unchanged — the most credible signal.** Per-operation allocation is deterministic and far less noisy than timing: −622 B (−0.0941%) for 6 months, +88 B (+0.0140%) for 1 month. The candidate does not alter per-operation memory behavior.
- **Gen0/1000 ops difference is an artifact.** Candidate reports 66.6667 vs baseline 58.8235 Gen0 collections per 1000 operations, but both runs had exactly one Gen0 collection during measurement; the normalized rate differs only because `Memory.TotalOperations` was 15 (candidate) vs 17 (baseline). Not a GC regression signal.
- No outliers were removed from either candidate benchmark (N=10 for both).
- These are **API server-side timings with the cache warm** — not browser paint, not end-to-end latency, and not first-process-startup cost.
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

- Run log: [raw/e01-candidate-warm.log](./raw/e01-candidate-warm.log)
- Per-run log: [raw/e01-candidate-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-210642.log](./raw/e01-candidate-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-210642.log)
- GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e01-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
- Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e01-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- CSV: [results/...DashboardBenchmarks-report.csv](./raw/e01-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
- HTML: [results/...DashboardBenchmarks-report.html](./raw/e01-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
