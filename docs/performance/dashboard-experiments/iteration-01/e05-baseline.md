# E05 Baseline (cold cache)

E05 baseline measurement for `GET api/Dashboard/overview` on the pre-candidate tree at HEAD `8f987e2e`. No source changes, no implementation, no comparisons in this document — raw numbers only. The candidate (precompute `GetStoredBondsIds` once instead of per account/day) was **not** implemented before or during this measurement.

## Command

Executed from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-baseline-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-baseline-cold.log 2>&1
```

## Exits / gate results

| Gate | Result |
|---|---|
| Process exit status (observed) | success — harness completion event reported clean completion (`state=completed`, no error output); see note below on `$?` capture |
| Benchmarks executed | `2` (`Run time: 00:00:11 (11.6 sec), executed benchmarks: 2`; global total `00:00:12 (12.02 sec), executed benchmarks: 2`) |
| Failures | none — both workloads completed all 10 actual iterations; no failed or outlier-dropped results (`AllOutliers` empty in full JSON) |
| First log output within 5 min | yes — first benchmark output appeared seconds after start; total run ~12 s |

**Note on exit-code capture:** the command ran through a background shell with all stdout/stderr redirected into `.log`, so `$?` was not echoed directly. Evidence for exit `0`: (1) harness completion event reported clean completion with no error output; (2) the full 195-line log contains zero errors/exceptions and ends at the normal termination path (`// * Artifacts cleanup * / Artifacts cleanup is finished`, reached only after a successful run); (3) the benchmark app's own run log in `raw/e05-baseline-cold/` ends with the same clean shutdown; (4) no macOS crash report was produced for the process.

## Results (N = 10, IterationCount=10, WarmupCount=3, InProcessEmitToolchain)

### `GET api/Dashboard/overview (6 months)`

| Metric | Value |
|---|---|
| Mean | 81.44 ms (81,443,704.15 ns) |
| Error (half of 99.9% CI) | 3.338 ms |
| StdDev | 2.208 ms |
| Median | 81.19 ms (81,190,005.125 ns) |
| P95 | 84.69 ms (84,689,653.05 ns) |
| N | 10 |
| Allocated / op | 5.33 MB (5,588,214 B; Gen0 = 750 per 1000 ops, Gen1 = 250 per 1000 ops, Gen2 = none) |

### `GET api/Dashboard/overview (1 month)`

| Metric | Value |
|---|---|
| Mean | 55.62 ms (55,619,925 ns) |
| Error (half of 99.9% CI) | 2.500 ms |
| StdDev | 1.654 ms |
| Median | 55.24 ms (55,241,369.75 ns) |
| P95 | 58.14 ms (58,135,774.0125 ns) |
| N | 10 |
| Allocated / op | 2.89 MB (3,030,266 B; Gen0 = 250 per 1000 ops, Gen1 = none) |

## Environment

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 1 CPU / 10 logical and physical cores
- .NET SDK 10.0.302; runtime .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- GC: Non-concurrent Workstation; HardwareIntrinsics: ArmBase+AdvSimd, AES, CRC32, DP, RDM, SHA1, SHA256, VectorSize=128
- Environment variables: `LANG=en_US.UTF-8`, `LC_ALL=en_US.UTF-8`, `FM_BENCH_RESEED=0`, `FM_BENCH_SEED_MONTHS=6`, `FM_BENCH_COLD_CACHE=1`

## Warnings observed in run log

- `! Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — benign on macOS; benchmark proceeded normally (emitted once per benchmark launch, 2 total).
- `warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3] Failed to determine the https port for redirect.`
- `warn: Microsoft.EntityFrameworkCore.Query[20504]` — query with multiple collection includes without `QuerySplittingBehavior` configured (EF default SingleQuery).

## Raw artifacts

- Console log: [raw/e05-baseline-cold.log](./raw/e05-baseline-cold.log)
- Benchmark run directory: `docs/performance/dashboard-experiments/iteration-01/raw/e05-baseline-cold/`
  - Full JSON report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub markdown report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md`](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv`](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html`](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)

## HEAD / seed hash verification (unchanged after run)

- HEAD before and after: `8f987e2e5f909716ece1d396f96c2701fbc3707a`
- Seed template SHA256 (`code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`) before and after: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`
- `git status` (both checkpoints): only the two preexisting modified sources (`code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs`, `code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs`) plus untracked `docs/performance/`. No concurrent dashboard benchmark was running during the measurement.
