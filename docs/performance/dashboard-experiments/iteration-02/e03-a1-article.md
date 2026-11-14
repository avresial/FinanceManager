# E03 iteration 02 — baseline A1 warm

Baseline at HEAD `8f987e2e5f909716ece1d396f96c2701fbc3707a`, with E03 candidate absent. This is one in-process API/SQLite run, not a browser or production measurement. The prior timed-out Qwen attempt's [empty log](raw/e03-a1-warm.log) remains intact; this recovery run uses new paths.

From `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-02/raw/e03-a1-terra-warm > ../../docs/performance/dashboard-experiments/iteration-02/raw/e03-a1-terra-warm.log 2>&1
```

Actual shell exit: **0** ([exit file](raw/e03-a1-terra-warm.exit)); the log reports two executed benchmarks and completion without benchmark failures.

| Window | Mean ms | Error ms | StdDev ms | Median ms | P95 ms | Allocated bytes/op | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 16.153318 | 0.644925 | 0.426578 | 16.044734 | 16.801216 | 660362 | 10 |
| 1 month | 14.146717 | 1.491484 | 0.986525 | 14.018966 | 15.437763 | 632942 | 10 |

Error is the 99.9% confidence interval half-width. BenchmarkDotNet 0.15.8, in-process emit, 10 iterations of 250 ms, 3 warmups; macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302 and .NET 10.0.10 Arm64. The log reports denied high process priority, unavailable HTTPS redirect port, and an EF Core multiple collection include warning.

Seed SHA256 before and after: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`. Git status before and after showed only the preexisting `GuestAccountSeeder.cs` and `BenchmarkEnvironment.cs` modifications and untracked `docs/performance/`; no candidate paths were modified. A pre-run content hash of the existing source diff was not captured, so byte-for-byte preservation of those preexisting edits cannot be independently demonstrated. No source edits were made by this checkpoint.

Evidence: [run log](raw/e03-a1-terra-warm.log), [full JSON](raw/e03-a1-terra-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [Markdown report](raw/e03-a1-terra-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md).
