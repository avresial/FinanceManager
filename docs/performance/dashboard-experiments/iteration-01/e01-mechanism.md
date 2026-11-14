# E01 bond history mechanism checkpoint

Unit tests exited 0. Benchmark command exited 0; all eight cases completed without errors. This isolates the bond-history mechanism and does not measure request context, API, or browser latency.

Fixture: 3,650-day-old bond; 31- and 183-day windows, ascending and descending. `RepeatedPointPricing` repeatedly calls `GetPriceAt`; `PreparedHistory` performs `GetPrice` with history preparation included on each invocation. Setup asserts per-date output and checksum parity.

Environment: BenchmarkDotNet 0.15.8, macOS Tahoe 26.6.2, Apple M1 Max, .NET SDK 10.0.302 / runtime 10.0.10 Arm64. Means and paired ratios are reported by BenchmarkDotNet; allocation ratios are relative to repeated point pricing.

| Window | Order | Method | Mean | Error | Median | P95 | Allocated | Paired mean ratio |
|---:|---|---|---:|---:|---:|---:|---:|---:|
|31|ascending|RepeatedPointPricing|14.5339 ms|0.2754 ms|14.5075 ms|14.8339 ms|15435.66 KB|1.00|
|31|ascending|PreparedHistory|0.4687 ms|0.0059 ms|0.4673 ms|0.4737 ms|498.89 KB|0.03|
|31|descending|RepeatedPointPricing|14.7999 ms|0.1123 ms|14.7999 ms|14.8968 ms|15436.57 KB|1.00|
|31|descending|PreparedHistory|0.4795 ms|0.0027 ms|0.4795 ms|0.4816 ms|498.90 KB|0.03|
|183|ascending|RepeatedPointPricing|85.6406 ms|0.3081 ms|85.6168 ms|85.8911 ms|90256.90 KB|1.000|
|183|ascending|PreparedHistory|0.4795 ms|0.0146 ms|0.4743 ms|0.4957 ms|498.90 KB|0.006|
|183|descending|RepeatedPointPricing|84.4090 ms|0.4487 ms|84.4470 ms|84.7696 ms|90256.76 KB|1.000|
|183|descending|PreparedHistory|0.4773 ms|0.0126 ms|0.4745 ms|0.4876 ms|498.89 KB|0.006|

Commands (both exit 0):

- `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj`
- `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet run -c Release -- --filter '*BondHistoryBenchmarks*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e01-mechanism`

Raw outputs: [unit test log](raw/e01-mechanism-tests.log), [benchmark console log](raw/e01-mechanism.log), [BenchmarkDotNet markdown](raw/e01-mechanism/results/FinanceManager.Benchmarks.Benchmarks.BondHistoryBenchmarks-report-github.md), [full JSON](raw/e01-mechanism/results/FinanceManager.Benchmarks.Benchmarks.BondHistoryBenchmarks-report-full.json).
