# E01 cold baseline (e01-baseline-cold)

Cache-bypassed dashboard benchmark run (`FM_BENCH_COLD_CACHE=1`, `FM_BENCH_RESEED=0`, `FM_BENCH_SEED_MONTHS=6`), measuring API server work for both dashboard cases with the cache bypassed. This is **not** a first process startup or browser latency measurement. Single measurement checkpoint; no source edits, no warm rerun, no validation rerun.

## Results

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | Gen1/1000 ops |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 88.79 ms | 6.403 ms | 4.235 ms | 89.54 ms | 93.77 ms | 5.31 MB | 750 | 250 |
| GET api/Dashboard/overview (1 month) | 55.74 ms | 2.526 ms | 1.671 ms | 55.66 ms | 57.94 ms | 2.89 MB | 250 | 0 |

Cross-checked against `report-full.json` (ns): 6-month mean 88,794,932.25 ns / median 89,541,750 ns / P95 93,774,129.65 ns / CI margin 6,402,517.5 ns; 1-month mean 55,739,767.775 ns / median 55,660,375.125 ns / P95 57,938,630.34 ns / CI margin 2,526,097.45 ns; bytes/op 5,571,170 (6-month) and 3,031,198 (1-month). Both match the report table.

Outliers: one lower outlier detected per benchmark (6-month: 79.81 ms; 1-month: 52.57 ms); no upper outliers.

## Environment (from report)

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83), Darwin 25.6.0
- Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- Toolchain=InProcessEmitToolchain, IterationCount=10, IterationTime=250ms, WarmupCount=3

## Command

Run from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-baseline-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-baseline-cold.log 2>&1
```

Exit code: **0** (global total time 00:00:12, executed benchmarks: 2; both benchmarks completed, no failures).

## Warnings / errors

No benchmark errors or exceptions. Log contains these non-fatal warnings:

- BenchmarkDotNet (x2): `Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — expected on macOS, does not affect measurements.
- ASP.NET Core `HttpsRedirectionMiddleware[3]`: `Failed to determine the https port for redirect.`
- EF Core `Query[20504]` (MultipleCollectionIncludeWarning): query loads related collections without configured `QuerySplittingBehavior`; defaults to `SingleQuery`.

## Reproducibility

- Git HEAD: `f3945df56fa0557e70de1859c70fc279faa30c54` (before and after run)
- Seed template SHA-256 (`bin/Release/net10.0/benchmark-data/seed-template.sqlite`, from benchmark project cwd): `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` — verified before the run (matched expected) and unchanged after the run (`FM_BENCH_RESEED=0`).

## Raw artifacts

- Log: [raw/e01-baseline-cold.log](./raw/e01-baseline-cold.log)
- Per-run log: [raw/e01-baseline-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-205226.log](./raw/e01-baseline-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-205226.log)
- GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e01-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
- Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e01-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- CSV: [results/...DashboardBenchmarks-report.csv](./raw/e01-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
- HTML: [results/...DashboardBenchmarks-report.html](./raw/e01-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
