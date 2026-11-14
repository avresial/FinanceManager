# E02 baseline (e02-baseline)

Baseline checkpoint for the four new `AccountRepository` routing cases and the actual currency/bond value-range implementations, measured before any E02 candidate edits. The parent reviewed those changes first; 2253 unit tests passed prior to this checkpoint. This run measures API server work only (in-process HTTP calls under BenchmarkDotNet) for both dashboard cases — it is **not** a browser-latency or first-process-startup measurement. Runs were executed sequentially with no concurrent benchmarking, and no source was modified by this checkpoint.

## Validation (all 48 calls)

Ran first, before the timing runs:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 dotnet run -c Release -- --validate > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-baseline-validate.log 2>&1
```

Exit code: **0**. Log ends with `All 48 benchmarked calls succeeded.` — all 48 validation rows report `ok`. The only line matching fail/error is the benign ASP.NET Core startup warning `Failed to determine the https port for redirect.` (same as E01), not a benchmark failure.

## Results

### Warm cache (`FM_BENCH_COLD_CACHE=0`)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | N |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 15.98 ms | 0.546 ms | 0.361 ms | 15.96 ms | 16.38 ms | 645.16 KB | 62.5 | 10 |
| GET api/Dashboard/overview (1 month) | 15.08 ms | 1.203 ms | 0.716 ms | 15.38 ms | 15.64 ms | 618.63 KB | 66.7 | 9 |

### Cold cache, bypassed (`FM_BENCH_COLD_CACHE=1`)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | Gen1/1000 ops | N |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 85.32 ms | 8.006 ms | 5.295 ms | 85.53 ms | 91.62 ms | 5.31 MB | 750 | 250 | 10 |
| GET api/Dashboard/overview (1 month) | 54.93 ms | 1.765 ms | 1.050 ms | 54.69 ms | 56.53 ms | 2.89 MB | 250 | 0 | 9 |

Cross-checked against `report-full.json` (ns, exact): warm 6-month mean 15,979,573.4375 / median 15,956,600.25 / P95 16,383,344.79 / CI margin 545,778.93 / bytes-op 660,642; warm 1-month mean 15,080,301.55 / median 15,384,588.93 / P95 15,643,792.21 / CI margin 1,202,768.22 / bytes-op 633,477; cold 6-month mean 85,319,074 / median 85,529,880.25 / P95 91,620,468.24 / CI margin 8,005,680.17 / bytes-op 5,571,464; cold 1-month mean 54,931,495.36 / median 54,688,947.75 / P95 56,526,849.95 / CI margin 1,764,976.75 / bytes-op 3,032,644. All match the report summary tables above.

Outliers (from log hints + JSON `LowerOutliers`/`UpperOutliers`):

- warm 6 months: none; N = 10.
- warm 1 month: hint "1 outlier was removed, 3 outliers were detected (13.71 ms, 14.01 ms, 17.02 ms)"; the value excluded from statistics is 17.02 ms (present in `WorkloadActual`, absent from JSON `OriginalValues`); N = 9 with lower outliers [13.707, 14.01] retained in `OriginalValues`.
- cold 6 months: 1 lower outlier detected at 76.311 ms (retained in statistics; N = 10).
- cold 1 month: hint "1 outlier was removed (58.97 ms)"; present in `WorkloadActual`, absent from JSON `OriginalValues`; N = 9, no outliers flagged on the retained set.

## Environment (from report)

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83), Darwin 25.6.0 — Apple M1 Max, 1 CPU, 10 logical and 10 physical cores (MacBookPro18,2)
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- GC=Non-concurrent Workstation
- Toolchain=InProcessEmitToolchain, IterationCount=10, IterationTime=250ms, WarmupCount=3

## Commands and exits (run sequentially from `code/FinanceManager.Benchmarks`)

1. Validation:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 dotnet run -c Release -- --validate > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-baseline-validate.log 2>&1
```

   Exit code: **0**.

2. Warm timing:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=0 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-baseline-warm > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-baseline-warm.log 2>&1
```

   Exit code: **0** (run time 00:00:10, global total time 00:00:11; executed benchmarks: 2).

3. Cold timing:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-baseline-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e02-baseline-cold.log 2>&1
```

   Exit code: **0** (run time 00:00:11, global total time 00:00:12; executed benchmarks: 2).

Each timing run executed exactly the two `*Dashboard*` benchmarks (`GET api/Dashboard/overview (6 months)`, `GET api/Dashboard/overview (1 month)`), and each exported a full report set (CSV, GitHub MD, HTML, full JSON); exit 0 alone was not treated as sufficient — generated files were inspected.

## Warnings / errors

No benchmark errors or exceptions in any of the three runs. Non-fatal warnings present in the timing logs:

- BenchmarkDotNet (per executed benchmark): `Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — expected on macOS, does not affect measurements.
- ASP.NET Core `HttpsRedirectionMiddleware[3]`: `Failed to determine the https port for redirect.`
- EF Core `Query[20504]` (MultipleCollectionIncludeWarning): query loads related collections without configured `QuerySplittingBehavior`; defaults to `SingleQuery`.

## Reproducibility

- Git HEAD: `8f987e2e5f909716ece1d396f96c2701fbc3707a` — recorded before the runs and unchanged after them (no commits made by this checkpoint).
- Seed template SHA-256 (`bin/Release/net10.0/benchmark-data/seed-template.sqlite`, from benchmark project cwd): `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` — verified before the runs (matched expected) and re-verified unchanged after the cold run (`FM_BENCH_RESEED=0`).
- Preexisting uncommitted edits preserved untouched: `code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs`, `code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs` (plus all docs).

## Raw artifacts

- Validation log: [raw/e02-baseline-validate.log](./raw/e02-baseline-validate.log)
- Warm log: [raw/e02-baseline-warm.log](./raw/e02-baseline-warm.log)
  - Per-run log: [raw/e02-baseline-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-003549.log](./raw/e02-baseline-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-003549.log)
  - Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e02-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e02-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV: [results/...DashboardBenchmarks-report.csv](./raw/e02-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML: [results/...DashboardBenchmarks-report.html](./raw/e02-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
- Cold log: [raw/e02-baseline-cold.log](./raw/e02-baseline-cold.log)
  - Per-run log: [raw/e02-baseline-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-003607.log](./raw/e02-baseline-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-003607.log)
  - Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e02-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e02-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV: [results/...DashboardBenchmarks-report.csv](./raw/e02-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML: [results/...DashboardBenchmarks-report.html](./raw/e02-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
