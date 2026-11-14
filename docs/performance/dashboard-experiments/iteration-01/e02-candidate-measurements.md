# E02 candidate measurements (e02-candidate)

Measurement checkpoint for the uncommitted E02 candidate — two production call changes in `NetWorthService`'s range overload (`includeEntryMetadata: false` on the currency/bond paths), plus the exact test update. Measured against the retained e02-baseline warm/cold results (see [e02-baseline.md](./e02-baseline.md)). API server work only (in-process HTTP calls under BenchmarkDotNet) — **not** a browser-latency, paint, or first-process-startup measurement. Runs executed sequentially with no concurrent benchmarking; this checkpoint made **no code edits**: candidate diff, test edits, preexisting `GuestAccountSeeder.cs`/`BenchmarkEnvironment.cs` edits, and all docs were preserved untouched before and after.

Prior to these runs the parent confirmed the exact unit-test run: 2254 passed / 0 failed (see [raw/e02-candidate-tests.log](./raw/e02-candidate-tests.log)).

## Validation (all 48 calls) — first artifact

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 dotnet run -c Release -- --validate > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-validate.log 2>&1
```

Exit code: **0**. Log ends with `All 48 benchmarked calls succeeded.` — all 48 validation rows report `ok`. The only fail/error-matching line is the benign ASP.NET Core startup warning `Failed to determine the https port for redirect.` (identical to the e02-baseline validate log), not a benchmark failure.

## Candidate results

### Warm cache (`FM_BENCH_COLD_CACHE=0`)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | N |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 17.99 ms | 3.123 ms | 2.066 ms | 17.30 ms | 21.20 ms | 644.25 KB | 66.6667 | 10 |
| GET api/Dashboard/overview (1 month) | 14.57 ms | 1.204 ms | 0.796 ms | 14.67 ms | 15.65 ms | 614.21 KB | 66.6667 | 10 |

### Cold cache, bypassed (`FM_BENCH_COLD_CACHE=1`)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | N |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 100.23 ms | 4.349 ms | 2.877 ms | 100.88 ms | 103.65 ms | 5.93 MB (6,213,748 B) | 500 | 10 |
| GET api/Dashboard/overview (1 month) | 67.35 ms | 3.786 ms | 2.504 ms | 67.42 ms | 70.47 ms | 3.27 MB (3,426,994 B) | 500 | 10 |

Cross-checked against `report-full.json` (ns, exact):

- warm 6 months: mean 17,989,198.346666668 / median 17,298,898.6 / P95 21,199,229.873333335 / CI margin 3,122,931.649465302 / StdDev 2,065,625.9269645836 / bytes-op 659,709 (N=10, no outliers)
- warm 1 month: mean 14,568,589.46 / median 14,673,884.733333334 / P95 15,647,845.853333334 / CI margin 1,203,884.353288104 / StdDev 796,294.9601041299 / bytes-op 628,948 (N=10, no outliers)
- cold 6 months: mean 100,234,445.8 / median 100,875,594 / P95 103,649,879 / CI margin 4,349,286.823961630 / StdDev 2,876,783.943996598 / bytes-op 6,213,748 (N=10, no outliers)
- cold 1 month: mean 67,348,648.975 / median 67,417,218.75 / P95 70,472,071.3625 / CI margin 3,786,295.090547180 / StdDev 2,504,399.770488755 / bytes-op 3,426,994 (N=10; one lower outlier detected at 63,036,270.75 ns ≈ 63.04 ms, retained in statistics)

Per-1000-ops GC rates are normalized by `Memory.TotalOperations`, which differs across runs (candidate: 15 and 15 warm; 2 and 4 cold), so they are informational only — not a regression signal.

## Baseline comparison (vs e02-baseline, same host)

Baseline source: [raw/e02-baseline-warm/results/...report-full.json](./raw/e02-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json) and [raw/e02-baseline-cold/results/...report-full.json](./raw/e02-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), exact values as recorded in [e02-baseline.md](./e02-baseline.md). Percentages computed from full-JSON ns/byte precision (candidate − baseline):

| Scenario | Metric | Baseline | Candidate | Δ |
|---|---|---:|---:|---:|
| warm, 6 months | Mean (ns) | 15,979,573.4375 | 17,989,198.346666668 | +2,009,624.909 ns (+12.5762%) |
| warm, 6 months | Median (ns) | 15,956,600.25 | 17,298,898.6 | +1,342,298.350 ns (+8.4122%) |
| warm, 6 months | P95 (ns) | 16,383,344.79 | 21,199,229.873333335 | +4,815,885.083 ns (+29.3950%) |
| warm, 6 months | Allocated/op (B) | 660,642 | 659,709 | −933 B (−0.1412%) |
| warm, 1 month | Mean (ns) | 15,080,301.55 | 14,568,589.46 | −511,712.090 ns (−3.3932%) |
| warm, 1 month | Median (ns) | 15,384,588.93 | 14,673,884.733333334 | −710,704.197 ns (−4.6196%) |
| warm, 1 month | P95 (ns) | 15,643,792.21 | 15,647,845.853333334 | +4,053.643 ns (+0.0259%) |
| warm, 1 month | Allocated/op (B) | 633,477 | 628,948 | −4,529 B (−0.7149%) |
| cold, 6 months | Mean (ns) | 85,319,074 | 100,234,445.8 | +14,915,371.800 ns (+17.4819%) |
| cold, 6 months | Median (ns) | 85,529,880.25 | 100,875,594 | +15,345,713.750 ns (+17.9419%) |
| cold, 6 months | P95 (ns) | 91,620,468.24 | 103,649,879 | +12,029,410.760 ns (+13.1296%) |
| cold, 6 months | Allocated/op (B) | 5,571,464 | 6,213,748 | +642,284 B (+11.5281%) |
| cold, 1 month | Mean (ns) | 54,931,495.36 | 67,348,648.975 | +12,417,153.615 ns (+22.6048%) |
| cold, 1 month | Median (ns) | 54,688,947.75 | 67,417,218.75 | +12,728,271.000 ns (+23.2739%) |
| cold, 1 month | P95 (ns) | 56,526,849.95 | 70,472,071.3625 | +13,945,221.413 ns (+24.6701%) |
| cold, 1 month | Allocated/op (B) | 3,032,644 | 3,426,994 | +394,350 B (+13.0035%) |

## Interpretation / uncertainty

Per configuration there is exactly one candidate run and one baseline run; the within-run 99.9% CIs below are used only as an overlap descriptor — **this is not a formal significance test** (no repeated runs, no run-to-run variance estimate).

- **Warm, 6 months — unresolved.** Mean delta +12.58% (+2.010 ms) exceeds the baseline's half-width (±0.546 ms) but sits well inside the candidate run's own wide margin (±3.123 ms; StdDev jumped to 2.066 ms vs 0.361 ms). Within-run CIs overlap: baseline [15.434, 16.525] ms vs candidate [14.866, 21.112] ms. The direction is not distinguishable from run noise.
- **Warm, 1 month — unresolved.** Mean delta −3.39% (−0.512 ms); the difference is smaller than both runs' CI half-widths and the CIs overlap: baseline [13.878, 16.283] ms vs candidate [13.365, 15.772] ms. Slightly-faster direction only, not established.
- **Cold, 6 months — unresolved, but the strongest signal of a regression.** Mean +17.48%, median +17.94%, P95 +13.13%: all three point the same way with no within-run CI overlap (baseline [77.313, 93.325] ms vs candidate [95.885, 104.584] ms). Per-operation allocation also rises +11.53% — a deterministic metric far less noisy than timing — which makes this the most credible substantive signal in the checkpoint (consistent with more data being loaded when the cache is bypassed; inference, not measured mechanism).
- **Cold, 1 month — unresolved, same pattern.** Mean +22.60%, median +23.27%, P95 +24.67% with no CI overlap (baseline [53.167, 56.696] ms vs candidate [63.562, 71.135] ms); allocation +13.00%.
- **Overall verdict: unresolved.** With one run per configuration the E02 candidate's performance effect is not established in either direction: warm results are mixed and inside noise (−3.4% / +12.6%), while both cold scenarios are consistently slower with higher allocation but lack repeated runs to separate a real regression from single-run variance. Repeated cold-cache runs would be needed before declaring the change safe or regressive.
- No outliers were removed in any candidate benchmark; all four have N = 10 (the one detected lower outlier on cold 1 month, ≈63.04 ms, was retained).

## Commands and exits (run sequentially from `code/FinanceManager.Benchmarks`)

1. Validation:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 dotnet run -c Release -- --validate > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-validate.log 2>&1
```

   Exit code: **0**; `All 48 benchmarked calls succeeded.`

2. Warm timing:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-warm > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-warm.log 2>&1
```

   Exit code: **0** (run time 00:00:10, global total time 00:00:11; executed benchmarks: 2).

3. Cold timing:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-cold.log 2>&1
```

   Exit code: **0** (run time 00:00:11, global total time 00:00:11; executed benchmarks: 2).

Each timing run executed exactly the two `*Dashboard*` benchmarks (`GET api/Dashboard/overview (6 months)`, `GET api/Dashboard/overview (1 month)`) and exported a full report set (CSV, GitHub MD, HTML, full JSON); exit 0 alone was not treated as sufficient — logs and generated files were inspected.

## Warnings / errors

No benchmark errors or exceptions in any of the three runs. Non-fatal warnings present in the timing logs (identical set to the e02-baseline runs):

- BenchmarkDotNet (per executed benchmark): `Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — expected on macOS, does not affect measurements.
- ASP.NET Core `HttpsRedirectionMiddleware[3]`: `Failed to determine the https port for redirect.`
- EF Core `Query[20504]` (MultipleCollectionIncludeWarning): query loads related collections without configured `QuerySplittingBehavior`; defaults to `SingleQuery`.

## Reproducibility

- Git HEAD: `8f987e2e5f909716ece1d396f96c2701fbc3707a` — recorded before the runs and unchanged after them (no commits made by this checkpoint).
- Seed template SHA-256 (`bin/Release/net10.0/benchmark-data/seed-template.sqlite`, from benchmark project cwd): `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` — verified before the runs (matched expected) and re-verified unchanged after the cold run (`FM_BENCH_RESEED=0`).
- Uncommitted work preserved untouched: E02 candidate `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs`, test edit `code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs`, preexisting `code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs` and `code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs`, plus all docs.

## Environment (from report)

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83), Darwin 25.6.0 — Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- GC=Non-concurrent Workstation
- Toolchain=InProcessEmitToolchain, IterationCount=10, IterationTime=250ms, WarmupCount=3

## Raw artifacts

- Validation log: [raw/e02-candidate-validate.log](./raw/e02-candidate-validate.log)
- Warm log: [raw/e02-candidate-warm.log](./raw/e02-candidate-warm.log)
  - Per-run log: [raw/e02-candidate-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-004543.log](./raw/e02-candidate-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-004543.log)
  - Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e02-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e02-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV: [results/...DashboardBenchmarks-report.csv](./raw/e02-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML: [results/...DashboardBenchmarks-report.html](./raw/e02-candidate-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
- Cold log: [raw/e02-candidate-cold.log](./raw/e02-candidate-cold.log)
  - Per-run log: [raw/e02-candidate-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-004602.log](./raw/e02-candidate-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-004602.log)
  - Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e02-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e02-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV: [results/...DashboardBenchmarks-report.csv](./raw/e02-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML: [results/...DashboardBenchmarks-report.html](./raw/e02-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
- Retained baseline results used for deltas: [e02-baseline-warm/results](./raw/e02-baseline-warm/results/) and [e02-baseline-cold/results](./raw/e02-baseline-cold/results/) (documented in [e02-baseline.md](./e02-baseline.md))
