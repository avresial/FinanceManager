# E01 long-held-bond cold candidate B1

HEAD `8f987e2e5f909716ece1d396f96c2701fbc3707a`. The retained E01 candidate patch applied cleanly and the diff for its three paths exactly matched the patch before and after the run. Candidate remains applied. The [preexisting tracked diff and validator addition](raw/e01-b1-preexisting.patch) and [hashes](raw/e01-b1-hashes.txt) were saved before application. All four preserved source hashes and both template hashes remained unchanged; no template WAL/SHM sidecars were found.

From `code/`, `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj` exited 0: **2259 passed, 0 failed, 0 skipped**. [Log](raw/e01-b1-tests.log), [exit](raw/e01-b1-tests.exit).

From `code/FinanceManager.Benchmarks`, with `FM_BENCH_DATA_DIR` set to the absolute `iteration-03/e01-data-63ac2cb175ab4e229d8fe34ed7b5bf63` directory, `FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet run -c Release -- --validate-e01-long-bond --e01-mode fixture --e01-output <absolute iteration-03/e01-candidate-report.json>` exited 0. Python JSON deep equality of the entire candidate report and [baseline fixture report](e01-fixture-report-1.json) passed, including scenario and every full/month response value and distribution, ignoring object property order. [Report](e01-candidate-report.json), [log](raw/e01-b1-validation.log), [exit](raw/e01-b1-validation.exit), [comparison](raw/e01-b1-parity.txt).

The benchmark ran sequentially from the same project directory with that absolute data directory and `FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8`:

```sh
dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-b1-cold > ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-b1-cold.log 2>&1
```

Exit **0**, two benchmarks executed, no benchmark errors. [Log](raw/e01-b1-cold.log), [exit](raw/e01-b1-cold.exit), [full JSON](raw/e01-b1-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [Markdown](raw/e01-b1-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md).

| Window | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated per operation | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 65.87 ms | 4.932 ms | 3.262 ms | 64.98 ms | 70.82 ms | 6.97 MB (7,307,242 bytes) | 10 |
| 1 month | 38.57 ms | 0.904 ms | 0.598 ms | 38.61 ms | 39.47 ms | 4.48 MB (4,702,541 bytes) | 10 |

Against [A1](e01-a1-article.md), mean changed from 186.27 to 65.87 ms (-64.6%) and 57.27 to 38.57 ms (-32.7%); allocation changed from 90.58 to 6.97 MB (-92.3%) and 19.46 to 4.48 MB (-77.0%), respectively. One A1/B1 pair does not establish confidence or reproducibility.

BenchmarkDotNet 0.15.8, in-process emit, 10 iterations of 250 ms, 3 warmups; macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302, .NET 10.0.10 Arm64. Priority elevation was denied; HTTPS redirect port and EF multiple-collection warnings appeared. This measures in-process API and SQLite with cache bypassed, not browser paint, network transport, or production latency. The long-held bond is synthetic. Prior fixture-control pricing validation covered only start and end; candidate parity above covers the complete reported responses, not independent pricing of every bucket. The original fixture builder's corrected version remains untested.
