# E03 iteration 02: B2 warm measurement

From `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-02/raw/e03-b2-warm > ../../docs/performance/dashboard-experiments/iteration-02/raw/e03-b2-warm.log 2>&1
```

Exit 0; two benchmarks executed. Statistics from the full JSON, times in milliseconds and allocation in bytes per operation; error is the 99.9% confidence interval margin.

| Window | Mean | Error | StdDev | Median | P95 | Allocated | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 16.290772 | 0.400810 | 0.265111 | 16.311089 | 16.664665 | 615042 | 10 |
| 1 month | 13.824295 | 0.740343 | 0.440566 | 13.897030 | 14.386055 | 583164 | 9 |

BenchmarkDotNet 0.15.8, Release, in-process emit, 3 warmups and 10 iterations of 250 ms; macOS Tahoe 26.6.2, Apple M1 Max, .NET 10.0.10 Arm64, SDK 10.0.302. One 1-month outlier was removed. High process priority could not be set (permission denied); HTTPS redirect port and EF Core multiple-collection query-splitting warnings were logged.

HEAD was `8f987e2e5f909716ece1d396f96c2701fbc3707a`. The applied two-file diff matched `iteration-01/e03-candidate.patch` byte for byte before and after measurement. Seed SHA256 was `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`; the seed and both preexisting modified source files passed recorded hash checks after measurement. Their original diff is retained in [preservation patch](raw/e03-b2-preservation.patch), with [hashes](raw/e03-b2-preservation.txt).

The two candidate files were restored from verified HEAD; their diffs were empty. From `code/`, `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj` exited 0: 2253 passed, 0 failed, 0 skipped. The two preexisting source modifications remain.

Raw evidence: [benchmark log](raw/e03-b2-warm.log), [exit](raw/e03-b2-warm.exit), [full JSON](raw/e03-b2-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [restoration test log](raw/e03-final-restoration-tests.log), [test exit](raw/e03-final-restoration-tests.exit).
