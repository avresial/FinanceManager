# E03 replacement warm benchmark

Replacement worker run: single **warm** Dashboard benchmark (`FM_BENCH_RESEED=0`, `FM_BENCH_COLD_CACHE=0`), 6-month seed, no reseeding. This is a standalone measurement of the candidate tree — no baseline comparison performed here.

## Command

Executed from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 \
  dotnet run -c Release -- --filter '*Dashboard*' \
    --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-replacement-warm \
  > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-replacement-warm.log 2>&1
```

## Exits / status

| Item | Value |
|---|---|
| `dotnet run` exit code | **0** |
| Executed benchmarks | 2 (no failures) |
| Global total time | 00:00:11 (11.28 s) + build/validate phase |

No benchmark failed. Only benign warnings observed in the log: process priority `High` not settable (`Permission denied`, harmless on macOS), ASP.NET Core "Failed to determine the https port for redirect", and an EF Core `QuerySplittingBehavior` compilation warning (all present in prior runs as well).

## Environment

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 1 CPU / 10 cores
- .NET SDK 10.0.302; Host: .NET 10.0.10, Arm64 RyuJIT armv8.0-a, RELEASE configuration
- Job `Job-QEVVIT`: Toolchain=InProcessEmitToolchain, IterationCount=10, IterationTime=250 ms, WarmupCount=3

## Results (both windows)

| Method | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated | Gen0 / 1k ops | N |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `GET api/Dashboard/overview (6 months)` | 14.748 ms | 0.293 ms | 0.194 ms | 14.728 ms | 15.049 ms | 599.72 KB (614,117 B) | 58.8235 | 10 |
| `GET api/Dashboard/overview (1 month)` | 13.864 ms | 1.183 ms | 0.783 ms | 13.813 ms | 14.781 ms | 570.82 KB (584,520 B) | 58.8235 | 10 |

Detail:

- **6 months**: Min 14.517 ms, Q1 14.604 ms, Max 15.065 ms, IQR 0.253 ms, CI99.9 [14.455; 15.041] ms (margin 0.293 ms = 1.99% of mean), Skewness 0.41, Kurtosis 1.6.
- **1 month**: Min 12.468 ms, Q1 13.443 ms, Max 14.888 ms, IQR 1.093 ms, CI99.9 [12.681; 15.048] ms (margin 1.183 ms = 8.54% of mean), Skewness −0.26, Kurtosis 1.64.

Values were verified consistent across the console log, `report-github.md`, and `report-full.json` (JSON stores ns; e.g. Mean 14747921.8 ns = 14.748 ms).

## Provenance

- Git HEAD at run time: `8f987e2e5f909716ece1d396f96c2701fbc3707a`
- Seed template hash (verified **before and after** the run, unchanged):
  `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` — `code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`
- No other Dashboard benchmark process was running during the measurement (no overlapping timing).

## Raw artifacts

- Log: [`raw/e03-replacement-warm.log`](./raw/e03-replacement-warm.log)
- Artifacts directory: [`raw/e03-replacement-warm/`](./raw/e03-replacement-warm/)
  - Full JSON report: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`](./raw/e03-replacement-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
  - Markdown summary: [`results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md`](./raw/e03-replacement-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
  - CSV / HTML reports and per-run log in the same `results/` directory
