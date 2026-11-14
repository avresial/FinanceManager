# E01 long-held-bond cold candidate B2

HEAD `8f987e2e5f909716ece1d396f96c2701fbc3707a`. The three candidate paths were clean before applying [the patch](../iteration-01/e01-candidate.patch); `git apply --check` succeeded, and the applied diff and patch both had SHA256 `fcc75e876bb3ca1222efecae8fb049abc8368c8a216fb295c42347d2a39eecfc` before and after the run. No concurrent benchmark process was found.

From `code/FinanceManager.Benchmarks`:

```sh
FM_BENCH_DATA_DIR=/Users/a2485/.t3/worktrees/FinanceManager/t3-8eea46f2/docs/performance/dashboard-experiments/iteration-03/e01-data-63ac2cb175ab4e229d8fe34ed7b5bf63 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-b2-cold > ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-b2-cold.log 2>&1
```

Exit **0**; two benchmarks executed without benchmark failures. [Exit](raw/e01-b2-cold.exit), [log](raw/e01-b2-cold.log), [full JSON](raw/e01-b2-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [Markdown](raw/e01-b2-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md).

| Window | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated per operation | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 71.075 ms | 3.906 ms | 2.584 ms | 71.883 ms | 74.288 ms | 7,277,214 bytes | 10 |
| 1 month | 38.254 ms | 0.852 ms | 0.507 ms | 38.353 ms | 38.830 ms | 4,700,948 bytes | 9 |

BenchmarkDotNet removed one one-month outlier (40.35 ms). BenchmarkDotNet 0.15.8 used in-process emit, 10 iterations of 250 ms and three warmups on macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302 and .NET 10.0.10 Arm64. Priority elevation was denied; HTTPS redirect port and EF multiple-collection warnings appeared. This is an in-process API/SQLite measurement with cache bypassed and a synthetic fixture. Earlier fixture-control pricing validation covered only start and end; the corrected fixture builder remains untested.

The four preserved files and fixture matched the [recorded hashes](raw/e01-a2-hashes.txt) before and after the run and after restoration testing. The seed SHA256 was `1fd1345066ad2a9e554082044e3fe9279d33d7404f0d84f6de7a20abf5c0d899`; no sidecars were present before or after the benchmark. After checking HEAD, only the three candidate paths were restored from HEAD; their diffs were empty.

From `code/`, `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj` exited **0**: 2,253 passed, zero failed or skipped. [Test log](raw/e01-restoration-final-tests.log), [exit](raw/e01-restoration-final-tests.exit).
