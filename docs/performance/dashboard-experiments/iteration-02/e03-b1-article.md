# E03 iteration 02 B1 — warm candidate

HEAD `8f987e2e5f909716ece1d396f96c2701fbc3707a`; candidate is the unchanged [iteration-01 patch](../iteration-01/e03-candidate.patch). `git apply --check` passed, and the resulting two-file diff matched the patch byte for byte both before and after measurement. Candidate remains applied.

From `code/`, `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj` exited 0: 2,258 passed, 0 failed, 0 skipped. [Test log](raw/e03-b1-tests.log), [exit](raw/e03-b1-tests.exit).

From `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-02/raw/e03-b1-warm
```

Exit 0; two benchmarks executed. Error is the half-width of the 99.9% confidence interval; allocations are managed bytes per operation. N is retained measurements.

| Window | Mean | Error | StdDev | Median | P95 | Allocated | N |
|---|---:|---:|---:|---:|---:|---:|---:|
| 6 months | 16.249 ms | 0.683 ms | 0.407 ms | 16.195 ms | 16.885 ms | 615,588 B | 9 |
| 1 month | 13.588 ms | 1.081 ms | 0.643 ms | 13.628 ms | 14.319 ms | 582,321 B | 9 |

BenchmarkDotNet 0.15.8, macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302, .NET 10.0.10 Arm64; in-process toolchain, 10 iterations of 250 ms and 3 warmups. High process priority could not be set (permission denied); HTTPS redirect port was undetermined; EF warned about multiple collection includes without configured query splitting. One outlier was removed for each window; two were detected for one month. No benchmark errors were reported.

[Full JSON](raw/e03-b1-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json) · [Markdown](raw/e03-b1-warm/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md) · [Log](raw/e03-b1-warm.log) · [Exit](raw/e03-b1-warm.exit)

The seed SHA256 remained `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`. The pre-existing GuestAccountSeeder and BenchmarkEnvironment SHA256 values remained `cfbb96b2f718a13ed5191e7f5c0669302d4147b6853ce577c97d08f3525b2a19` and `65da47133609eabb20b2d3cc68fa2becc756c3fd1f87581a23594f152949e17e`, respectively. The initial source diff and hashes are retained in [preservation evidence](raw/e03-b1-preservation.txt).
