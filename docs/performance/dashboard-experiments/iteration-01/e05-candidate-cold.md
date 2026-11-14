# E05 Candidate Cold Measurement

Single cold-cache measurement of the candidate `NetWorthService` change, run against the existing
E05 baseline. Test sub-agent role: measure only; no source changes, no commits, no restoration/reseed.

## Pre-flight gates (all passed)

- HEAD verified: `8f987e2e5f909716ece1d396f96c2701fbc3707a`.
- Two-file candidate diff (`NetWorthService.cs`, `NetWorthServiceTests.cs`) byte-identical to
  [`e05-candidate.patch`](./e05-candidate.patch) (both 5243 bytes, `cmp` clean).
- Seed template hash verified: `code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`
  SHA256 `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`.
- No concurrent benchmark processes (`pgrep -fl` empty) before the run.
- Artifact/log/exit paths confirmed new (did not exist) before execution.

## Command and exit status

Run from `code/FinanceManager.Benchmarks`:

```sh
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 \
  dotnet run -c Release -- --filter '*Dashboard*' \
    --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-candidate-cold \
    > ../../docs/performance/dashboard-experiments/iteration-01/raw/e05-candidate-cold.log 2>&1
```

Exit status captured immediately after completion into a task-specific shell variable and written to
[`raw/e05-candidate-cold.exit`](./raw/e05-candidate-cold.exit): **`0`**.

Completion gate: log ends with `Global total time: 00:00:11 (11.87 sec), executed benchmarks: 2`; no failed
benchmark lines in the log (only environment warnings below). Requirement met: exit 0 AND 2 executed / 0 failures.

## Environment

Identical to the baseline run (`raw/e05-baseline-cold`):

- BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.2 [Darwin 25.6.0], Apple M1 Max (10 cores), .NET SDK 10.0.302,
  runtime .NET 10.0.10 Arm64 RyuJIT, GC=Non-concurrent Workstation.
- Job: `InProcessEmitToolchain`, IterationCount=10, IterationTime=250ms, WarmupCount=3 (N = 10 per benchmark).
- Cold cache run (`FM_BENCH_COLD_CACHE=1`), no reseed (`FM_BENCH_RESEED=0`), seed months = 6.

Warnings present in the log (environmental/informational, same class as baseline):

- `Failed to set up priority High for process ... Permission denied` (macOS process-priority restriction).
- ASP.NET `HttpsRedirectionMiddleware`: "Failed to determine the https port for redirect."
- EF Core `MultipleCollectionIncludeWarning` (default SingleQuery splitting).

## Comparison: candidate cold vs baseline cold

Exact values from the full JSON reports; deltas are exact-precision percentages, `(candidate − baseline)/baseline`.

### GET api/Dashboard/overview (6 months) — `Overview_SixMonths`, N = 10

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Mean | 81,443,704.15 ns (81.44 ms) | 81,228,638.60 ns (81.23 ms) | −0.264067% |
| Error (99.9% CI half-width) | 3,338,207.252762312 ns (3.34 ms) | 10,236,208.697495745 ns (10.24 ms) | +206.637902% |
| StdDev | 2,208,017.4095605547 ns (2.21 ms) | 6,770,618.2692111805 ns (6.77 ms) | +206.637902% |
| Median | 81,190,005.125 ns (81.19 ms) | 80,314,776.125 ns (80.31 ms) | −1.078001% |
| P95 | 84,689,653.05 ns (84.69 ms) | 89,228,008.3625 ns (89.23 ms) | +5.358807% |
| Allocated per operation | 5,588,214 B (5.33 MB) | 5,543,768 B (5.29 MB) | −0.795353% |

### GET api/Dashboard/overview (1 month) — `Overview_OneMonth`, N = 10

| Metric | Baseline | Candidate | Delta |
|---|---:|---:|---:|
| Mean | 55,619,925 ns (55.62 ms) | 54,992,782.30 ns (54.99 ms) | −1.127550% |
| Error (99.9% CI half-width) | 2,499,973.2591809477 ns (2.50 ms) | 2,540,302.978738412 ns (2.54 ms) | +1.613206% |
| StdDev | 1,653,577.5228274628 ns (1.65 ms) | 1,680,253.135263416 ns (1.68 ms) | +1.613206% |
| Median | 55,241,369.75 ns (55.24 ms) | 54,503,458.375 ns (54.50 ms) | −1.335795% |
| P95 | 58,135,774.0125 ns (58.14 ms) | 57,034,428.6375 ns (57.03 ms) | −1.894437% |
| Allocated per operation | 3,030,266 B (2.89 MB) | 3,014,070 B (2.87 MB) | −0.534475% |

Candidate summary table (from its own markdown report): 6-month mean 81.23 ms / alloc 5.29 MB;
1-month mean 54.99 ms / alloc 2.87 MB.

No significance claim is made from CI overlap: candidate and baseline confidence intervals overlap for both
benchmarks, and the mean deltas (−0.26% at 6 months, −1.13% at 1 month) are well inside run-to-run noise for a
single N=10 cold measurement.

## Raw artifacts

Candidate (this run):

- [`raw/e05-candidate-cold.log`](./raw/e05-candidate-cold.log) — full console log
- [`raw/e05-candidate-cold.exit`](./raw/e05-candidate-cold.exit) — captured exit status (`0`)
- [`raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- [`raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md`](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-github.md)
- [`raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv`](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.csv)
- [`raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html`](./raw/e05-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report.html)
- [`raw/e05-candidate-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-135922.log`](./raw/e05-candidate-cold/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-135922.log)

Baseline (comparison reference):

- [`raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json`](./raw/e05-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- [`raw/e05-baseline-cold.log`](./raw/e05-baseline-cold.log)

## Limitations

1. **Baseline exit status was not directly captured**: no `e05-baseline-cold.exit` exists; baseline completion is
   evidenced by its artifacts and log (`executed benchmarks: 2`). Retained as a documented limitation.
2. Single cold measurement per side (N = 10 iterations each); the 6-month candidate run shows elevated variance
   (StdDev ≈ 3× baseline), so mean deltas are not statistically interpretable — no significance claim is made from CI overlap.
3. Environment warnings (process-priority permission denied, HTTPS redirect port, EF Core single-query warning)
   apply to both runs and are not attributable to the candidate change.

## Post-run integrity check

- `git status` shows only the four expected modified files plus untracked `docs/performance/`; the two-file candidate
  diff re-computed after the run is still byte-identical to `e05-candidate.patch`.
- Seed template SHA256 unchanged: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`.

Candidate change remains applied for parent review. No warm or other tests/benchmarks were run.
