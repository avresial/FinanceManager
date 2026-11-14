# E06 — `GET api/TransactionLog/Get/{userId}`: one-endpoint cold-cache measurement

**Status: ALREADY IMPLEMENTED / no new candidate.** The optimization originally proposed for this
endpoint is present in the source at HEAD. No A/B before/after comparison was manufactured, and no
optimization edit (or rollback) was made for this experiment. One clean measurement of the current
implementation under cold-cache conditions is reported below.

## Why there is no candidate change

The parent task's original discovery that `TransactionLogService.GetLastTransactions` fetched
entries per-account / resolved bond names one-by-one is **stale**. The current source at HEAD
already contains the batched implementation:

- `code/FinanceManager.Application/Dashboard/TransactionLogService.cs`
  - line 44 — currency entries via `currencyEntryRepository.GetMostRecentByAccounts(...)`
  - line 61 — bond entries via `bondEntryRepository.GetMostRecentByAccounts(...)`
  - line 81 — investment transactions via `investmentTransactionRepository.GetMostRecentByAccounts(...)`
  - lines 245–257 — `GetBondNames` resolves missing names in a single
    `bondDetailsRepository.GetNamesByIdsAsync(missingIds, ...)` call (no per-id lookups)

- `code/FinanceManager.Infrastructure/Features/FinancialAccounts/Currencies/Repositories/CurrencyEntryRepository.cs`
  lines 599–631 — `GetMostRecentByAccounts`: filters with
  `.Where(e => accountIdChunk.Contains(e.AccountId))` over chunks of distinct account IDs, orders
  `OrderByDescending(PostingDate).ThenByDescending(EntryId)`, projects a slim DTO via `.Select(...)` and
  applies `.Take(count)` — all on the `IQueryable`, so filtering, ordering, projection and `TOP` are
  pushed to the database **before** materialization (`.ToListAsync`).

- `code/FinanceManager.Infrastructure/Features/FinancialAccounts/Bond/Repositories/BondEntryRepository.cs`
  lines 614–641 — `GetMostRecentByAccounts`: identical shape — chunked account-ID filter, server-side
  ordering, slim `.Select(...)` projection and `.Take(count)` before materialization.

Existing unit tests already pin this behavior:

- `code/FinanceManager.Tests.Unit/Application/Services/TransactionLogServiceTests.cs` lines 154–250:
  - lines 154–172 — bond name resolution is batched through one `GetNamesByIdsAsync` call and falls
    back for missing details (`GetByIdAsync` verified never called);
  - lines 174–205 — only the user's available account IDs are passed to each batch query;
  - lines 207–247 — entry queries are not issued while the accounts stream is still open (scoped
    `DbContext` single-active-query safety).

All evidence matches the parent's report; no contradictions were found.

## Measurement (current implementation, cold cache)

Single BenchmarkDotNet run of the endpoint benchmark
(`*SupportingReadBenchmarks.TransactionLog_Get*`), cold cache, six-month demo dataset:

| Method | Mean | Error | StdDev | Median | P95 | Gen0 | Allocated |
|---|---:|---:|---:|---:|---:|---:|---:|
| `GET api/TransactionLog/Get/{userId}` | 6.664 ms | 0.1100 ms | 0.0727 ms | 6.657 ms | 6.764 ms | 41.6667 | 322.87 KB |

- N = 10 iterations; exactly **1** benchmark executed, no failures (run log:
  `executed benchmarks: 1`).
- Allocated per operation: 330,616 B ≈ 322.87 KB; Gen0 collections 2 over 48 operations.

### Environment

- macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0], Apple M1 Max, 1 CPU / 10 physical + logical cores
- .NET SDK 10.0.302; runtime .NET 10.0.10, Arm64 RyuJIT armv8.0-a; configuration `RELEASE`
- BenchmarkDotNet 0.15.8, InProcessEmitToolchain, IterationCount=10, IterationTime=250 ms, WarmupCount=3

### Exact command and reproducibility

Run from `code/FinanceManager.Benchmarks`:

```bash
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 \
  dotnet run -c Release -- \
    --filter '*SupportingReadBenchmarks.TransactionLog_Get*' \
    --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/e06-existing-cold
```

Shell exit code: **0** (captured in `raw/e06-existing-cold.exit`). Seed template unchanged, verified by hash:
`code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite`
SHA-256 = `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`. HEAD at run time:
`8f987e2e5f909716ece1d396f96c2701fbc3707a`; working tree contained only the preserved changes to
`GuestAccountSeeder.cs`, `BenchmarkEnvironment.cs` and untracked `docs/performance/`. No concurrent
benchmarks were running.

### Raw artifacts (repo-relative)

- Run log: [`raw/e06-existing-cold.log`](raw/e06-existing-cold.log)
- Exit code: [`raw/e06-existing-cold.exit`](raw/e06-existing-cold.exit)
- Results dir: `raw/e06-existing-cold/results/` — full JSON, GitHub Markdown, CSV and HTML reports

## Scope limits

This measures only the current six-month demo dataset with the default (unscaled) account count.
Scaled-account workloads and browser-side rendering were not measured here. No before/after gain is
claimed or implied; this run documents the existing optimized implementation as-is. No code changes,
rollbacks, or further optimization work are required for E06.
