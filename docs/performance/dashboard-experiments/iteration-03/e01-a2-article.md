# E01 long-held-bond cold baseline A2

HEAD `8f987e2e5f909716ece1d396f96c2701fbc3707a`. Before restoration the three candidate paths' diff and [candidate patch](../iteration-01/e01-candidate.patch) had identical SHA256 `fcc75e876bb3ca1222efecae8fb049abc8368c8a216fb295c42347d2a39eecfc`. The [full tracked diff](raw/e01-a2-preservation.patch) was saved. Only those three candidate paths were restored using `git restore --source=HEAD --` and their exact paths; their diffs were empty immediately after restoration and after the benchmark. The four preserved source/helper files and fixture matched the [recorded hashes](raw/e01-a2-hashes.txt) after restoration and after the run. The fixture SHA256 was `1fd1345066ad2a9e554082044e3fe9279d33d7404f0d84f6de7a20abf5c0d899`; no sidecars were present before or after.

From `code/FinanceManager.Benchmarks`:

```sh
FM_BENCH_DATA_DIR=/Users/a2485/.t3/worktrees/FinanceManager/t3-8eea46f2/docs/performance/dashboard-experiments/iteration-03/e01-data-63ac2cb175ab4e229d8fe34ed7b5bf63 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-a2-cold > ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-a2-cold.log 2>&1
```

Exit **0**, two benchmarks executed, no benchmark errors. [Exit](raw/e01-a2-cold.exit), [log](raw/e01-a2-cold.log), [full JSON](raw/e01-a2-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [Markdown](raw/e01-a2-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md).

| Window | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated per operation | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 183.533 ms | 10.560 ms | 6.985 ms | 183.256 ms | 192.874 ms | 94,891,008 bytes | 10 |
| 1 month | 56.166 ms | 0.795 ms | 0.416 ms | 56.111 ms | 56.769 ms | 20,414,056 bytes | 8 |

BenchmarkDotNet removed two one-month outliers (58.56 and 61.74 ms). BenchmarkDotNet 0.15.8 used in-process emit, 10 iterations of 250 ms and three warmups on macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302 and .NET 10.0.10 Arm64. Priority elevation was denied; HTTPS redirect port and EF multiple-collection warnings appeared. This is an in-process API/SQLite measurement with cache bypassed and a synthetic fixture, not browser or production latency. Earlier fixture-control pricing validation covered only start and end; the corrected fixture builder remains untested.
