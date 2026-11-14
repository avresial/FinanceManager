# E03 Replacement — Cold Dashboard Benchmark

Single cold-cache dashboard measurement of the applied replacement candidate (e03). No warm or validation reruns performed. Parent owns comparison and confirmation design; earlier collided `raw/e03-candidate-warm` reports were preserved untouched.

## Command

Run from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 \
  dotnet run -c Release -- --filter '*Dashboard*' \
  --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-replacement-cold \
  > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-replacement-cold.log 2>&1
```

## Exits / Preconditions

| Check | Value |
|---|---|
| Process exit code | **0** |
| Benchmarks executed | 2 (log: `executed benchmarks: 2`) |
| Failures | none (only benign env notices, see Warnings) |
| Git HEAD before/after | `8f987e2e5f909716ece1d396f96c2701fbc3707a` (expected match) |
| Seed template SHA-256 (before and after) | `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` — unchanged |
| Source status | unchanged: same 4 modified files + untracked `docs/performance/`; candidate stays applied |
| Overlapping benchmark process | none present before run; output paths did not pre-exist |

## Results (cold cache, N = 10 samples per benchmark)

Job: `InProcessEmitToolchain, IterationCount=10, IterationTime=250ms, WarmupCount=3`

| Method | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated/op |
|---|---:|---:|---:|---:|---:|---:|
| GET api/Dashboard/overview (6 months) | **84.69 ms** | 7.220 ms | 4.775 ms | 85.66 ms | 90.06 ms | 5.26 MB |
| GET api/Dashboard/overview (1 month) | **56.30 ms** | 3.035 ms | 2.008 ms | 55.99 ms | 59.42 ms | 2.84 MB |

Additional stats from the full JSON report:

- **6 months**: Min 76.915 ms, Max 90.416 ms, Q1 81.601 ms, Q3 87.961 ms; CI [77.466 ms; 91.905 ms]; Skewness −0.43, Kurtosis 1.54; Gen0 750/1000 ops, Gen1 250/1000 ops; BytesAllocatedPerOperation = 5,519,678 B (≈ 5.26 MiB).
- **1 month**: Min 53.980 ms, Max 60.587 ms, Q1 54.712 ms, Q3 57.142 ms; CI [53.260 ms; 59.330 ms]; Skewness 0.70, Kurtosis 2.52; Gen0 250/1000 ops, Gen1 0; BytesAllocatedPerOperation = 2,982,468 B (≈ 2.84 MiB).

## Environment

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 10 cores
- .NET SDK 10.0.302; runtime .NET 10.0.10 Arm64 RyuJIT armv8.0-a
- GC: Non-concurrent Workstation
- Seed months: 6 (`FM_BENCH_SEED_MONTHS=6`), no reseed (`FM_BENCH_RESEED=0`), cold cache forced (`FM_BENCH_COLD_CACHE=1`)

## Warnings (benign, non-failing)

- `Failed to set up priority High for process ... Permission denied` — macOS does not allow setting process priority; standard on this host.
- ASP.NET Core `HttpsRedirectionMiddleware`: "Failed to determine the https port for redirect."
- EF Core warning 20504: multiple-collection `Include` without explicit `QuerySplittingBehavior`; informational, pre-existing in app queries.

## Raw artifacts

- Log: [`raw/e03-replacement-cold.log`](./raw/e03-replacement-cold.log)
- Full JSON: [`raw/e03-replacement-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`](./raw/e03-replacement-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- GitHub markdown: [`raw/e03-replacement-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md`](./raw/e03-replacement-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
- CSV / HTML reports also present in the same `results/` directory.

## Stop state

Candidate remains applied; no warm/validation/test reruns and no further experiments were performed. Comparison and confirmation are owned by the parent agent.
