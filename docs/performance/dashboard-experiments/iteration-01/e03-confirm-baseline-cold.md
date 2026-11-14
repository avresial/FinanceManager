# E03 — Cold Baseline Confirmation (single measurement, post-restoration = HEAD code)

Date: 2026-10-31 · Actor: test sub-agent (E03 cold baseline confirmation task)

## 1. Pre-run gates

Candidate already restored by the prior E03 restoration step (2253 tests passed); no
source changes, reseeding, or further restoration performed in this run. `FM_BENCH_RESEED=0`.

| Gate | Result |
|---|---|
| HEAD | `8f987e2e5f909716ece1d396f96c2701fbc3707a` ✓ (matches required) |
| Diff of candidate files vs HEAD (`git diff --stat`) | empty for both `AssetsServiceInvestment.cs` and `AssetsServiceInvestmentTests.cs` ✓ — restored state confirmed, proceed |
| Seed template SHA256 (pre-run) | `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` ✓ (`code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`) |
| Concurrent dashboard benchmark | none running (`ps` check) ✓ |
| Output paths new (no overwrite) | `raw/e03-confirm-baseline-cold{,.log}` did not preexist ✓ |
| Preserved worktree state | `GuestAccountSeeder.cs` and `BenchmarkEnvironment.cs` modifications intact; all `docs/performance/` patches/docs untouched ✓ |

## 2. Exact command and execution gates

Run from `code/FinanceManager.Benchmarks/`:

```
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 \
  dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-confirm-baseline-cold \
  > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-confirm-baseline-cold.log 2>&1
```

| Gate | Result |
|---|---|
| Exit code | **0** ✓ |
| Executed benchmarks / failures | **executed benchmarks: 2, no failures** ✓ (log tail: `Global total time: 00:00:12 (12.13 sec), executed benchmarks: 2`) |

## 3. Results (cold cache, FM_BENCH_COLD_CACHE=1)

| Method | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated | Gen0 / 1k ops | Samples (N) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `GET api/Dashboard/overview (6 months)` | **83.88 ms** | 6.404 ms | 4.236 ms | 83.63 ms | 89.21 ms | 5.37 MB | 750 | 10 measurement iterations (WarmupCount=3) |
| `GET api/Dashboard/overview (1 month)` | **57.18 ms** | 3.260 ms | 2.156 ms | 57.39 ms | 60.07 ms | 2.89 MB | 250 | 10 measurement iterations (WarmupCount=3) |

Raw values from the full JSON report (`Statistics`, ns):
- 6 months: Mean 83,882,158.3 · StdErr 1,339,444.8 · Median/P50 83,632,546.8 · P95 89,206,964.1 · N=10 (min 76,124,364.5 / max 89,328,843.8)
- 1 month: Mean 57,177,350.0 · StdErr 681,924.0 · Median/P50 57,385,369.8 · P95 60,073,976.0 · N=10 (min 54,186,645.8 / max 60,783,447.8)

Allocated bytes per operation from JSON `Memory.BytesAllocatedPerOperation`:
- 6 months: 5,633,284 B/op (≈ 5.37 MB) · Gen1 1 collection over run
- 1 month: 3,033,976 B/op (≈ 2.89 MB)

## 4. Environment

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 1 CPU, 10 logical / 10 physical cores
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- Toolchain `InProcessEmitToolchain`, IterationCount=10, IterationTime=250 ms, WarmupCount=3

## 5. Warnings observed in the run log (environment noise, not measurement failures)

- `// ! Failed to set up priority High for process … Permission denied` (BenchmarkDotNet host note, benign on macOS; appears around both benchmark sections)
- `warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3] — Failed to determine the https port for redirect.` (app startup in-process)
- `warn: Microsoft.EntityFrameworkCore.Query[20504]` — MultipleCollectionInclude / query-splitting advisory during EF query compilation

No BenchmarkDotNet warnings on the measurements themselves; no failed benchmarks.

## 6. Raw artifacts

- Log: [raw/e03-confirm-baseline-cold.log](raw/e03-confirm-baseline-cold.log)
- Artifacts dir (created new): [raw/e03-confirm-baseline-cold/](raw/e03-confirm-baseline-cold/) — `FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-132628.log` and `results/` with report-full.json / -github.md / .csv / .html

## 7. Post-run integrity (final re-verification)

| Check | Result |
|---|---|
| HEAD after run | `8f987e2e5f909716ece1d396f96c2701fbc3707a` ✓ unchanged |
| Seed template SHA256 after run (`shasum -a 256 …/seed-template.sqlite`) | `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` ✓ unchanged (no reseed, as expected with FM_BENCH_RESEED=0) |
| Worktree after run | only `GuestAccountSeeder.cs` and `BenchmarkEnvironment.cs` modified + untracked `docs/performance/`; candidate files still empty-diff vs HEAD ✓ |

No comparison against other runs performed (out of scope). No source changes, no reseeding, no
other tests or benchmarks. Parent writes the final article and decides E04 after review. STOP.
