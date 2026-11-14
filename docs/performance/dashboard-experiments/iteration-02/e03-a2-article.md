# E03 A2 warm baseline

HEAD was `8f987e2e5f909716ece1d396f96c2701fbc3707a`. Before restoration, the two candidate files' diff matched [the retained candidate patch](../iteration-01/e03-candidate.patch) byte for byte. The [full tracked diff](raw/e03-a2-preservation.patch) and [source and seed SHA-256 hashes](raw/e03-a2-preservation.txt) were saved before restoring only those two files with `git restore --source=HEAD -- <paths>`. Their diff against HEAD was empty after restoration and after the run. The two preserved source hashes and seed hash were unchanged after restoration and after the run; the seed SHA-256 was `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`.

From `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-02/raw/e03-a2-warm > ../../docs/performance/dashboard-experiments/iteration-02/raw/e03-a2-warm.log 2>&1
```

Process [exit](raw/e03-a2-warm.exit): 0; two benchmarks executed. Values below come from the [full JSON](raw/e03-a2-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json); error is the half-width of the 99.9% confidence interval, and allocation is bytes per operation.

| Window | Mean ms | Error ms | StdDev ms | Median ms | P95 ms | Alloc B/op | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 17.3815 | 0.4258 | 0.2534 | 17.3478 | 17.7659 | 661840 | 9 |
| 1 month | 14.0463 | 1.2820 | 0.7629 | 13.8833 | 15.0640 | 631079 | 9 |

BenchmarkDotNet 0.15.8, in-process Release, macOS Tahoe 26.6.2, Apple M1 Max, .NET 10.0.10 Arm64, SDK 10.0.302. Ten iterations and three warmups were configured; one outlier was removed from each result. The [log](raw/e03-a2-warm.log) reports inability to raise process priority, an HTTPS redirect port warning, and an EF Core multiple-collection query splitting warning. These are in-process API/SQLite measurements, not browser timings.
