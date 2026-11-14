# E01 warm baseline (e01-baseline-warm)

Warm-cache dashboard benchmark run (`FM_BENCH_COLD_CACHE=0`, `FM_BENCH_RESEED=0`, `FM_BENCH_SEED_MONTHS=6`), measuring API server work for both dashboard cases. Single measurement checkpoint; no source edits, no cold run.

## Results

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops |
|---|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 15.73 ms | 0.355 ms | 0.235 ms | 15.76 ms | 16.07 ms | 645.46 KB | 58.8235 |
| GET api/Dashboard/overview (1 month) | 13.84 ms | 1.176 ms | 0.778 ms | 13.66 ms | 15.12 ms | 615.31 KB | 58.8235 |

Cross-checked against `report-full.json` (ns): 6-month mean 15,732,403.9 ns / median 15,758,678.9 ns / P95 16,069,998.4 ns / CI margin 355,476.9 ns; 1-month mean 13,838,024.8 ns / median 13,659,989.0 ns / P95 15,117,209.2 ns / CI margin 1,176,490.1 ns; bytes/op 660,948 (6-month) and 630,074 (1-month). Both match the report table.

## Environment (from report)

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83), Darwin 25.6.0
- Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- Toolchain=InProcessEmitToolchain, IterationCount=10, IterationTime=250ms, WarmupCount=3

## Command

Run from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_COLD_CACHE=0 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-baseline-warm > ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-baseline-warm.log 2>&1
```

Exit code: **0** (completed in ~11.4 s of benchmark time; both benchmarks executed, no failures).

## Warnings / errors

No benchmark errors or exceptions. Log contains these non-fatal warnings:

- BenchmarkDotNet (x2): `Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — expected on macOS, does not affect measurements.
- ASP.NET Core `HttpsRedirectionMiddleware[3]`: `Failed to determine the https port for redirect.`
- EF Core `Query[20504]` (MultipleCollectionIncludeWarning): query loads related collections without configured `QuerySplittingBehavior`; defaults to `SingleQuery`.

## Reproducibility

- Git HEAD: `f3945df56fa0557e70de1859c70fc279faa30c54`
- Seed template SHA-256 (`bin/Release/net10.0/benchmark-data/seed-template.sqlite`, from benchmark project cwd): `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`

## Raw artifacts

- Log: [raw/e01-baseline-warm.log](./raw/e01-baseline-warm.log)
- Per-run log: [raw/e01-baseline-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-205008.log](./raw/e01-baseline-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261030-205008.log)
- GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e01-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
- Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e01-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- CSV: [results/...DashboardBenchmarks-report.csv](./raw/e01-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
- HTML: [results/...DashboardBenchmarks-report.html](./raw/e01-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
