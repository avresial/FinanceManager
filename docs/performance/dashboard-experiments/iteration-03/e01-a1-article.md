# E01 long-held-bond cold baseline A1

HEAD `8f987e2e5f909716ece1d396f96c2701fbc3707a`; candidate was not applied. The three candidate paths `BondDashboardContext.cs`, `NetWorthService.cs`, and `BondDashboardContextTests.cs` were unchanged against HEAD before and after the run. No candidate comparison has been made.

From `code/FinanceManager.Benchmarks`, with no concurrent benchmark process, the one run was:

```sh
export FM_BENCH_DATA_DIR=/Users/a2485/.t3/worktrees/FinanceManager/t3-8eea46f2/docs/performance/dashboard-experiments/iteration-03/e01-data-63ac2cb175ab4e229d8fe34ed7b5bf63 FM_BENCH_COLD_CACHE=1 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8
dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-a1-cold > ../../docs/performance/dashboard-experiments/iteration-03/raw/e01-a1-cold.log 2>&1
```

Command exit **0**; BenchmarkDotNet reported **2 executed benchmarks**, with no benchmark errors. [Log](raw/e01-a1-cold.log), [exit](raw/e01-a1-cold.exit), [full JSON](raw/e01-a1-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [Markdown](raw/e01-a1-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md).

| Overview window | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated per operation | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 186.27 ms | 9.064 ms | 5.995 ms | 185.02 ms | 195.00 ms | 90.58 MB (94,982,416 bytes) | 10 |
| 1 month | 57.27 ms | 1.701 ms | 1.125 ms | 57.33 ms | 58.77 ms | 19.46 MB (20,410,042 bytes) | 10 |

BenchmarkDotNet 0.15.8, in-process emit toolchain, 10 iterations of 250 ms, 3 warmups; macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302, .NET 10.0.10 Arm64. The log reports failure to raise process priority (permission denied), failure to determine an HTTPS redirect port, and EF Core's multiple-collection include/single-query warning. These qualify interpretation of timings.

The fixture and original template SHA-256 values before and after were respectively `1fd1345066ad2a9e554082044e3fe9279d33d7404f0d84f6de7a20abf5c0d899` and `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`; no template WAL/SHM sidecars were observed. The original is `code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`.

This measures the in-process API overview and SQLite work with cache bypassed, including response consumption, not browser paint, network transport, or production latency. The long-held bond is synthetic and not production representative. Repository selection covered entry 7 on all 170 days, aged 3650 days at the start; fixture-control contribution matched calculated pricing within 0.01 at the full start and shared end. Intermediate buckets were not independently checked. See [fixture validation](e01-fixture-validation.md). The original fixture builder exited 1 after commit; its corrected version remains untested.
