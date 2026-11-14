# E03 — Exact Restoration + One Warm Baseline Measurement

Date: 2026-10-31 · Actor: test sub-agent (E03 restoration task)

## 1. Restoration proof

| Gate | Result |
|---|---|
| HEAD before restore | `8f987e2e5f909716ece1d396f96c2701fbc3707a` ✓ (matches required) |
| Live diff of candidate files vs `e03-candidate.patch` | byte-identical (`cmp` OK, 16594-byte patch covering both candidate files) — no mismatch, proceed |
| Pre-restoration worktree snapshot | full `git diff` (344 lines, all 4 modified files incl. unrelated two) saved to [evidence/e03-pre-restoration-worktree.patch](evidence/e03-pre-restoration-worktree.patch) before any restore |
| Seed template SHA256 pre-restore | `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` ✓ |

Authorized restoration executed (exact command):

```
git restore --source=8f987e2e5f909716ece1d396f96c2701fbc3707a \
  -- code/FinanceManager.Application/FinancialAccounts/Investments/Assets/AssetsServiceInvestment.cs \
     code/FinanceManager.Tests.Unit/Application/Services/AssetsServiceInvestmentTests.cs
```

Post-restore verification:

| Check | Result |
|---|---|
| `git diff` of the two candidate files | empty (0 bytes) ✓ — exactly restored to HEAD |
| `GuestAccountSeeder.cs` unchanged | SHA256 identical pre/post restore (`cfbb96b2…`), still modified in worktree ✓ |
| `BenchmarkEnvironment.cs` unchanged | SHA256 identical pre/post restore (`65da4713…`), still modified in worktree ✓ |
| Candidate code/tests retained | yes — full candidate diff preserved in [e03-candidate.patch](e03-candidate.patch) (byte-identical to the live diff before restore); not re-applied, not deleted |
| Worktree after restore | only the two unrelated files modified + untracked `docs/performance/` ✓ |

## 2. Unit tests (post-restoration)

Command (run from `code/`, per CLAUDE.md MTP runner requirements):

```
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj \
  > ../docs/performance/dashboard-experiments/iteration-01/raw/e03-restoration-tests.log 2>&1
```

| Gate | Result |
|---|---|
| Exit code | **0** ✓ |
| Passed / total | **2253 passed, 0 failed, 0 skipped** (total 2253) ✓ |
| Duration | 28.077 s (test run 27.446 s) |

Raw log: [raw/e03-restoration-tests.log](raw/e03-restoration-tests.log)

## 3. Warm baseline benchmark (single measurement, post-restoration = HEAD code)

Pre-run checks: no other dashboard benchmark process running; output paths
`raw/e03-confirm-baseline-warm{,.log}` did not preexist. Seed SHA256 re-verified immediately before the run (`c2f4978d…`, unchanged). `FM_BENCH_RESEED=0` — no reseeding.

Exact command (run from `code/FinanceManager.Benchmarks/`):

```
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 \
  dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-confirm-baseline-warm \
  > ../../docs/performance/dashboard-experiments/iteration-01/raw/e03-confirm-baseline-warm.log 2>&1
```

| Gate | Result |
|---|---|
| Exit code | **0** ✓ |
| Executed benchmarks / failures | **executed benchmarks: 2, no failures** ✓ (per log tail `Global total time: 00:00:11 … executed benchmarks: 2`) |

### Results (warm, FM_BENCH_COLD_CACHE=0)

| Method | Mean | Error (99.9% CI half-width) | StdDev | Median | P95 | Allocated | Gen0 / 1k ops | Samples (N) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `GET api/Dashboard/overview (6 months)` | **15.54 ms** | 0.455 ms | 0.301 ms | 15.40 ms | 15.96 ms | 645.9 KB | 62.5 | 10 measurement iterations (WarmupCount=3; memory over 16 ops) |
| `GET api/Dashboard/overview (1 month)` | **13.96 ms** | 1.273 ms | 0.842 ms | 13.54 ms | 15.02 ms | 615.71 KB | 58.8 | 10 measurement iterations (WarmupCount=3; memory over 17 ops) |

Raw values from the full JSON report (`Statistics.OriginalValues`, ns):
- 6 months: Mean 15,537,873.4 ns · Median/P50 15,396,339.8 ns · P95 15,963,510.4 ns · N=10 (min 15,274,270.8 / max 15,977,502.6)
- 1 month: Mean 13,955,344.6 ns · Median/P50 13,540,212.0 ns · P95 15,019,177.5 ns · N=10 (min 13,163,098.0 / max 15,040,583.4)

### Environment

- BenchmarkDotNet v0.15.8
- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 1 CPU, 10 logical / 10 physical cores
- .NET SDK 10.0.302; Host: .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
- Toolchain `InProcessEmitToolchain`, IterationCount=10, IterationTime=250 ms, WarmupCount=3

### Warnings observed in the run log (environment noise, not measurement failures)

- `// ! Failed to set up priority High for process … Permission denied` (BenchmarkDotNet host note, benign on macOS; appears around both benchmark sections)
- `warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3] — Failed to determine the https port for redirect.` (app startup in-process)
- `warn: Microsoft.EntityFrameworkCore.Query[20504]` — MultipleCollectionInclude / query-splitting advisory during EF query compilation

No BenchmarkDotNet warnings on the measurements themselves; no failed benchmarks.

### Raw artifacts

- Log: [raw/e03-confirm-baseline-warm.log](raw/e03-confirm-baseline-warm.log)
- Artifacts dir (created new): [raw/e03-confirm-baseline-warm/](raw/e03-confirm-baseline-warm/) — `FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-20261031-132307.log` and `results/` with report-full.json / -github.md / .csv / .html

## 4. Seed integrity (final re-verification)

```
shasum -a 256 code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite
c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867  (expected c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867) ✓ unchanged
```

No baseline comparison performed (out of scope). No cold runs, no reseeding, no other measurements. Candidate patch retained; worktree left with only the two unrelated pre-existing modifications plus untracked `docs/performance/`. STOP.
