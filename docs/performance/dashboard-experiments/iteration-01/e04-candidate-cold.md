# E04 Candidate (cold cache)

E04 candidate measurement for `GET api/Dashboard/overview` with the two-file candidate applied (`LiabilitiesService.cs`, `LiabilitiesServiceTests.cs`). Numbers below are API server-side benchmark work (in-process request handling), not browser latency. Compared against the cold baseline in [e04-baseline.md](./e04-baseline.md) using exact values from the full JSON reports; relative deltas only — no formal significance claim is made from CI overlap.

## Command

Executed from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e04-candidate-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e04-candidate-cold.log 2>&1
```

## Exits / gate results

| Gate | Result |
|---|---|
| Process exit code (actual) | `0` |
| Benchmarks executed | `2` (`Run time: 00:00:11 (11.8 sec), executed benchmarks: 2`; global total `00:00:12 (12.15 sec), executed benchmarks: 2`) |
| Failures | none — both workloads completed all actual iterations and produced statistics; no failed benchmark |
| First log output within 5 min | yes — first benchmark output appeared seconds after start; total run ~12 s |

Note on N for the 1-month window: BenchmarkDotNet removed 1 outlier (57.37 ms) from that workload's statistics, so its reported `N = 9` (6 months is `N = 10`). This is an outlier-trimming decision by the harness, not a failed iteration.

## Results — candidate (exact values from report-full.json)

### `GET api/Dashboard/overview (6 months)`

| Metric | Value |
|---|---|
| Mean | 85.11 ms (85,111,654.175 ns) |
| Error (half of 99.9% CI) | 4.334 ms (4,334,363.887720306 ns) |
| StdDev | 2.867 ms (2,866,913.3456 ns) |
| Median | 85.35 ms (85,350,718.625 ns) |
| P95 | 89.36 ms (89,363,152.725 ns) |
| N | 10 |
| Allocated / op | 5.33 MB (5,588,158 B; Gen0 = 750 per 1000 ops, Gen1 = 250 per 1000 ops) |

### `GET api/Dashboard/overview (1 month)`

| Metric | Value |
|---|---|
| Mean | 54.37 ms (54,368,212.9444 ns) |
| Error (half of 99.9% CI) | 2.004 ms (2,004,335.0633 ns) |
| StdDev | 1.193 ms (1,192,747.6469 ns) |
| Median | 54.18 ms (54,183,156.25 ns) |
| P95 | 56.06 ms (56,058,397.95 ns) |
| N | 9 (1 outlier removed: 57.37 ms) |
| Allocated / op | 2.89 MB (3,027,864 B; Gen0 = 250 per 1000 ops, Gen1 = none) |

## Comparison against cold baseline (`raw/e04-baseline-cold` full JSON)

Exact baseline values and relative deltas, candidate vs baseline:

| Window | Metric | Baseline (exact ns) | Candidate (exact ns) | Relative delta |
|---|---|---:|---:|---:|
| 6 months | Mean | 82,488,713.575 | 85,111,654.175 | **+3.179757%** (slower) |
| 6 months | Error | 6,546,674.0355 | 4,334,363.887720306 | −33.792887% |
| 6 months | StdDev | 4,330,219.5312 | 2,866,913.3456 | −33.792887% |
| 6 months | Median | 83,498,578.25 | 85,350,718.625 | +2.218170% (slower) |
| 6 months | P95 | 87,144,068.675 | 89,363,152.725 | +2.546454% (slower) |
| 6 months | Allocated / op | 5,575,776 B | 5,588,158 B | +0.222068% |
| 1 month | Mean | 56,039,354.175 | 54,368,212.9444 | **−2.982085%** (faster) |
| 1 month | Error | 3,508,656.9721 | 2,004,335.0633 | −42.874579% |
| 1 month | StdDev | 2,320,759.3454 | 1,192,747.6469 | −48.605285% |
| 1 month | Median | 55,863,385.375 | 54,183,156.25 | −3.007747% (faster) |
| 1 month | P95 | 59,447,875.9875 | 56,058,397.95 | −5.701597% (faster) |
| 1 month | Allocated / op | 3,034,172 B | 3,027,864 B | −0.207899% |

Per-window N: baseline 6 months = 10, candidate 6 months = 10; baseline 1 month = 10, candidate 1 month = 9 (outlier removal noted above).

### CI overlap (context only, not a significance claim)

- 6 months: baseline 99.9% CI [75,942,039.5395; 89,035,387.6105] ns vs candidate [80,777,290.2873; 89,446,018.0627] ns — overlapping intervals.
- 1 month: baseline 99.9% CI [52,530,697.2029; 59,548,011.1471] ns vs candidate [52,363,877.8811; 56,372,548.0078] ns — overlapping intervals.

Per the experiment protocol, no formal significance conclusion is drawn from these overlaps; the deltas above are descriptive only. The direction differs by window: candidate mean/median/P95 are higher in the 6-month window and lower in the 1-month window, while per-op allocation is within ±0.23% in both windows.

## Environment

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 1 CPU / 10 logical and physical cores
- .NET SDK 10.0.302; runtime .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- GC: Non-concurrent Workstation; HardwareIntrinsics: ArmBase+AdvSimD, AES, CRC32, DP, RDM, SHA1, SHA256, VectorSize=128
- Toolchain InProcessEmitToolchain, IterationCount=10 (time-based 250 ms), WarmupCount=3
- Environment variables: `LANG=en_US.UTF-8`, `LC_ALL=en_US.UTF-8`, `FM_BENCH_RESEED=0`, `FM_BENCH_SEED_MONTHS=6`, `FM_BENCH_COLD_CACHE=1`

## Warnings observed in run log

- `! Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions. Message: Permission denied` — benign on macOS; benchmark proceeded normally.
- `warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3] Failed to determine the https port for redirect.`
- `warn: Microsoft.EntityFrameworkCore.Query[20504]` — query with multiple collection includes without `QuerySplittingBehavior` configured (EF default SingleQuery).
- Outlier hint: `'GET api/Dashboard/overview (1 month)' -> 1 outlier was removed (57.37 ms)` (affects N for that window only).

## Raw artifacts

- Console log: [raw/e04-candidate-cold.log](./raw/e04-candidate-cold.log)
- Benchmark run directory: `docs/performance/dashboard-experiments/iteration-01/raw/e04-candidate-cold/`
  - Full JSON report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`](./raw/e04-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub markdown report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md`](./raw/e04-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv`](./raw/e04-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html`](./raw/e04-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)

## Pre/post verification (unchanged after run)

- HEAD before and after: `8f987e2e5f909716ece1d396f96c2701fbc3707a`
- Seed template SHA256 (`code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`) before and after: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`
- Candidate diff (`LiabilitiesService.cs`, `LiabilitiesServiceTests.cs`) re-verified against [`e04-candidate.patch`](./e04-candidate.patch) after the run: exact match, still applied.
- `git status` (both checkpoints): modified sources are exactly the two preexisting isolation changes (`code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs`, `code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs`) plus the two candidate files, and untracked `docs/performance/`. No other source changed.
- No concurrent dashboard benchmark was running during the measurement (process check before start).
