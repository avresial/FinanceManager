# E04 Baseline (cold cache)

E04 baseline measurement for `GET api/Dashboard/overview` on the unmodified candidate-less tree. No source changes, no implementation, no comparisons in this document — raw numbers only.

## Command

Executed from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e04-baseline-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e04-baseline-cold.log 2>&1
```

## Exits / gate results

| Gate | Result |
|---|---|
| Process exit code (actual) | `0` |
| Benchmarks executed | `2` (`Run time: 00:00:11 (11.73 sec), executed benchmarks: 2`; global total `00:00:12 (12.13 sec), executed benchmarks: 2`) |
| Failures | none — both workloads completed all 10 actual iterations; no failed/outlier-dropped results (`AllOutliers` empty) |
| First log output within 5 min | yes — first benchmark output appeared seconds after start; total run ~12 s |

## Results (N = 10, IterationCount=10, WarmupCount=3, InProcessEmitToolchain)

### `GET api/Dashboard/overview (6 months)`

| Metric | Value |
|---|---|
| Mean | 82.49 ms (82,488,713.575 ns) |
| Error (half of 99.9% CI) | 6.547 ms |
| StdDev | 4.330 ms |
| Median | 83.50 ms |
| P95 | 87.14 ms (87,144,068.675 ns) |
| N | 10 |
| Allocated / op | 5.32 MB (5,575,776 B; Gen0 = 750 per 1000 ops, Gen1 = 250 per 1000 ops) |

### `GET api/Dashboard/overview (1 month)`

| Metric | Value |
|---|---|
| Mean | 56.04 ms (56,039,354.175 ns) |
| Error (half of 99.9% CI) | 3.509 ms |
| StdDev | 2.321 ms |
| Median | 55.86 ms |
| P95 | 59.45 ms (59,447,875.9875 ns) |
| N | 10 |
| Allocated / op | 2.89 MB (3,034,172 B; Gen0 = 250 per 1000 ops, Gen1 = none) |

## Environment

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 1 CPU / 10 logical and physical cores
- .NET SDK 10.0.302; runtime .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- GC: Non-concurrent Workstation; HardwareIntrinsics: ArmBase+AdvSimD, AES, CRC32, DP, RDM, SHA1, SHA256, VectorSize=128
- Environment variables: `LANG=en_US.UTF-8`, `LC_ALL=en_US.UTF-8`, `FM_BENCH_RESEED=0`, `FM_BENCH_SEED_MONTHS=6`, `FM_BENCH_COLD_CACHE=1`

## Warnings observed in run log

- `! Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — benign on macOS; benchmark proceeded normally.
- `warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3] Failed to determine the https port for redirect.`
- `warn: Microsoft.EntityFrameworkCore.Query[20504]` — query with multiple collection includes without `QuerySplittingBehavior` configured (EF default SingleQuery).

## Raw artifacts

- Console log: [raw/e04-baseline-cold.log](./raw/e04-baseline-cold.log)
- Benchmark run directory: `docs/performance/dashboard-experiments/iteration-01/raw/e04-baseline-cold/`
  - Full JSON report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`](./raw/e04-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub markdown report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md`](./raw/e04-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv`](./raw/e04-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html`](./raw/e04-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)

## HEAD / seed hash verification (unchanged after run)

- HEAD before and after: `8f987e2e5f909716ece1d396f96c2701fbc3707a`
- Seed template SHA256 (`code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`) before and after: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`
- `git status` (both checkpoints): only the two preexisting modified sources (`code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs`, `code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs`) plus untracked `docs/performance/`. No concurrent dashboard benchmark was running during the measurement.
