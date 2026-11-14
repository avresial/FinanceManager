# E03 baseline (e03-baseline)

Baseline checkpoint before the parent-selected E03 first candidate (request-local reuse of investment endpoint values between type/account asset distributions), which is **not implemented yet** at this point. The retained harness edits (`GuestAccountSeeder.cs` seedMonths support, `BenchmarkEnvironment.cs` CreateHostScope) are preserved; all tests and docs from the restored E01/E02 candidates remain committed (2253 unit tests passed previously). This run measures API server work only (in-process HTTP calls under BenchmarkDotNet) for both dashboard cases — it is **not** a browser-latency or first-process-startup measurement. Runs were executed sequentially with no concurrent benchmarking, and no source was modified by this checkpoint (no code changes, commits, restores, subagents, or other experiments).

## Validation (all 48 calls)

Ran first, before the timing runs:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 dotnet run -c Release -- --validate > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-baseline-validate.log 2>&1
```

Exit code: **0**. Log ends with `All 48 benchmarked calls succeeded.` — all 48 validation rows report `ok`. No exception or stack-trace lines in the log; the only line matching fail/error is the benign ASP.NET Core startup warning `Failed to determine the https port for redirect.` (same as E01/E02), not a benchmark failure.

## Results

### Warm cache (`FM_BENCH_COLD_CACHE=0`)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | N |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 15.69 ms | 0.407 ms | 0.269 ms | 15.74 ms | 16.02 ms | 645.83 KB | 66.6667 | 10 |
| GET api/Dashboard/overview (1 month) | 15.50 ms | 1.380 ms | 0.913 ms | 15.55 ms | 16.79 ms | 613.65 KB | 62.5000 | 10 |

### Cold cache, bypassed (`FM_BENCH_COLD_CACHE=1`)

| Case | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op | Gen0/1000 ops | Gen1/1000 ops | N |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | 89.10 ms | 11.236 ms | 6.686 ms | 93.53 ms | 94.57 ms | 5.35 MB | 750 | 250 | 9 |
| GET api/Dashboard/overview (1 month) | 56.35 ms | 3.164 ms | 2.093 ms | 55.76 ms | 59.57 ms | 2.89 MB | 250 | – | 10 |

Cross-checked against `report-full.json` (ns, exact): warm 6-month mean 15,685,433.05 / median 15,738,826.40 / P95 16,015,294.57 / CI margin 406,900.80 / bytes-op 661,333; warm 1-month mean 15,497,289.84 / median 15,550,100.25 / P95 16,787,829.42 / CI margin 1,379,879.51 / bytes-op 628,380; cold 6-month mean 89,101,752.31 / median 93,530,833.25 / P95 94,573,864.55 / CI margin 11,236,120.75 / bytes-op 5,613,744; cold 1-month mean 56,346,403.08 / median 55,759,239.50 / P95 59,574,048.00 / CI margin 3,163,768.57 / bytes-op 3,028,910. All match the report summary tables above.

Outliers (from log hints + JSON `LowerOutliers`/`UpperOutliers`):

- warm 6 months: hint "1 outlier was detected (15.15 ms)"; retained in statistics — present in `OriginalValues`, listed as a lower outlier in the JSON; N = 10.
- warm 1 month: no outliers flagged; N = 10.
- cold 6 months: hint "1 outlier was removed (107.79 ms)"; value absent from `OriginalValues`; N = 9, no outliers on the retained set.
- cold 1 month: no outliers flagged; N = 10.

## Environment (from report / logs)

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83), Darwin 25.6.0 — Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- GC=Non-concurrent Workstation
- Toolchain=InProcessEmitToolchain, IterationCount=10, IterationTime=250ms, WarmupCount=3

## Commands and exits (run sequentially from `code/FinanceManager.Benchmarks`)

1. Validation:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 dotnet run -c Release -- --validate > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-baseline-validate.log 2>&1
```

   Exit code: **0**.

2. Warm timing:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-baseline-warm > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-baseline-warm.log 2>&1
```

   Exit code: **0** (run time 00:00:10, global total time 00:00:11; executed benchmarks: 2).

3. Cold timing:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-baseline-cold > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-baseline-cold.log 2>&1
```

   Exit code: **0** (run time 00:00:12, global total time 00:00:12; executed benchmarks: 2).

Each timing run executed exactly the two `*Dashboard*` benchmarks (`GET api/Dashboard/overview (6 months)`, `GET api/Dashboard/overview (1 month)`), and each exported a full report set (CSV, GitHub MD, HTML, full JSON); exit 0 alone was not treated as sufficient — generated files were inspected. Total elapsed time across all three runs stayed far inside the fifteen-minute budget.

## Warnings / errors

No benchmark errors or exceptions in any of the three runs. Non-fatal warnings present in the timing logs:

- BenchmarkDotNet (per executed benchmark): `Failed to set up priority High for process System.Diagnostics.Process (FinanceManager.Benchmarks). Make sure you have the right permissions.` — expected on macOS, does not affect measurements.
- ASP.NET Core `HttpsRedirectionMiddleware[3]`: `Failed to determine the https port for redirect.`

## Reproducibility

- Git HEAD: `8f987e2e5f909716ece1d396f96c2701fbc3707a` — recorded before the runs and unchanged after them (no commits made by this checkpoint).
- Seed template SHA-256 (`bin/Release/net10.0/benchmark-data/seed-template.sqlite`, from benchmark project cwd): `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` — verified before the runs (matched expected) and re-verified unchanged after the cold run (`FM_BENCH_RESEED=0`).
- Worktree diff at checkpoint start: [evidence/e03-baseline-worktree.patch](./evidence/e03-baseline-worktree.patch) (3,385 bytes; only the two retained harness edits).
- Preexisting uncommitted edits preserved untouched: `code/FinanceManager.Application/Identity/Seeders/GuestAccountSeeder.cs`, `code/FinanceManager.Benchmarks/Hosting/BenchmarkEnvironment.cs` (plus all docs).

## Raw artifacts

- Validation log: [raw/e03-baseline-validate.log](./raw/e03-baseline-validate.log)
- Warm log: [raw/e03-baseline-warm.log](./raw/e03-baseline-warm.log)
  - Per-run log: [raw/e03-baseline-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-005819.log](./raw/e03-baseline-warm/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-005819.log)
  - Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e03-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e03-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV: [results/...DashboardBenchmarks-report.csv](./raw/e03-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML: [results/...DashboardBenchmarks-report.html](./raw/e03-baseline-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
- Cold log: [raw/e03-baseline-cold.log](./raw/e03-baseline-cold.log)
  - Per-run log: [raw/e03-baseline-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-005835.log](./raw/e03-baseline-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-005835.log)
  - Full JSON: [results/...DashboardBenchmarks-report-full.json](./raw/e03-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - GitHub report: [results/...DashboardBenchmarks-report-github.md](./raw/e03-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV: [results/...DashboardBenchmarks-report.csv](./raw/e03-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
  - HTML: [results/...DashboardBenchmarks-report.html](./raw/e03-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
