# E04 — Cache enumerated end-of-range currency accounts per service instance

Status: measured, restored. Recommendation (parent): **DO NOT ADOPT**. No demonstrated benefit; the candidate adds same-scope cache freshness risk without a general performance win.

## Hypothesis and change

Clarified hypothesis: `AccountRepository` already caches/deduplicates identical `(end, end)` reads, so a dashboard overview that asks for liabilities per account **and** per type does not pay two database queries — the residual cost is repeated async enumeration of the same result plus grouping over the streamed sequence. The candidate therefore removes only the residual enumeration/grouping work; it makes no claim of eliminating DB access.

Exact change (two files, full diff in [`e04-candidate.patch`](./e04-candidate.patch)):

1. `code/FinanceManager.Application/MoneyFlow/NetWorth/LiabilitiesService.cs` — added an instance-local `Dictionary<(int UserId, DateTime End), List<CurrencyAccount>> _endAccounts`; a helper `GetEndAccountsAsync(userId, end)` that on miss fully enumerates the exact existing call `financialAccountService.GetAccounts<CurrencyAccount>(userId, end, end)` and stores the list only after successful enumeration (failed reads are retried next call). Both `GetEndLiabilitiesPerAccount` and `GetEndLiabilitiesPerType` now iterate/synchronize over that shared list. Grouping stays over the complete input so first-occurrence group order is unchanged; no prefilter, no date truncation (`start` remains unused as before); `includeEntryMetadata` default unchanged. `IsAnyAccountWithLiabilities` and `GetLiabilitiesTimeSeries` untouched.
2. `code/FinanceManager.Tests.Unit/Application/Services/LiabilitiesServiceTests.cs` — four existing tests preserved verbatim; appended fixture + three new tests (4 test cases): both call orders on one instance produce identical rows with exactly one repository call for the exact key; per-user and exact-end (+1 minute) key isolation, including a fresh instance re-reading an identical key; failed enumeration is not cached and retry re-enumerates.

## Measurements

Cold cache only (server-side, in-process API work on SQLite — no browser latency). Times are milliseconds; allocations are bytes per operation. Error is BenchmarkDotNet's within-run 99.9% interval half-width, not a between-run significance test. Single run each of baseline and candidate, same seed (`FM_BENCH_RESEED=0`), six-month seed data, English locale, `DashboardBenchmarks` filter. Absolute values below are taken directly from the full JSON reports in [raw/e04-baseline-cold](./raw/e04-baseline-cold/) and [raw/e04-candidate-cold](./raw/e04-candidate-cold/).

| Run | Window | Mean (ms) | Error (ms) | Median (ms) | P95 (ms) | Allocated (B/op) | N |
|---|---|---:|---:|---:|---:|---:|---:|
| baseline-cold | 6 mo | 82.489 | 6.547 | 83.499 | 87.144 | 5,575,776 | 10 |
| candidate-cold | 6 mo | 85.112 | 4.334 | 85.351 | 89.363 | 5,588,158 | 10 |
| baseline-cold | 1 mo | 56.039 | 3.509 | 55.863 | 59.448 | 3,034,172 | 10 |
| candidate-cold | 1 mo | 54.368 | 2.004 | 54.183 | 56.058 | 3,027,864 | 9 (1 outlier removed: 57.37 ms) |

Relative deltas, candidate vs baseline (exact ns from the full JSON reports):

| Window | Metric | Baseline (ns) | Candidate (ns) | Relative delta |
|---|---|---:|---:|---:|
| 6 mo | Mean | 82,488,713.575 | 85,111,654.175 | **+3.179757%** (slower) |
| 6 mo | Error | 6,546,674.0355 | 4,334,363.887720306 | −33.79% |
| 6 mo | Median | 83,498,578.25 | 85,350,718.625 | +2.218170% (slower) |
| 6 mo | P95 | 87,144,068.675 | 89,363,152.725 | +2.546454% (slower) |
| 6 mo | Allocated | 5,575,776 B | 5,588,158 B | +0.222068% |
| 1 mo | Mean | 56,039,354.175 | 54,368,212.9444 | **−2.982085%** (faster) |
| 1 mo | Error | 3,508,656.9721 | 2,004,335.0633 | −42.87% |
| 1 mo | Median | 55,863,385.375 | 54,183,156.25 | −3.007747% (faster) |
| 1 mo | P95 | 59,447,875.9875 | 56,058,397.95 | −5.701597% (faster) |
| 1 mo | Allocated | 3,034,172 B | 3,027,864 B | −0.207899% |

CI overlap (descriptive context only — per protocol this is not a significance test): both windows have overlapping 99.9% intervals between baseline and candidate (6 mo: [75,942,039.5; 89,035,387.6] vs [80,777,290.3; 89,446,018.1] ns; 1 mo: [52,530,697.2; 59,548,011.1] vs [52,363,877.9; 56,372,548.0] ns).

## Interpretation and parent conclusion

- With a single run per side, the direction differs by window: the candidate is slower in the 6-month window (mean +3.18%, median +2.22%, P95 +2.55%) and faster in the 1-month window (mean −2.98%, median −3.01%, P95 −5.70%). Allocations are effectively flat in both windows (±0.23%).
- No general benefit is proven: a single baseline/candidate pair cannot establish that either window's delta is repeatable, and the conflicting directions plus overlapping CIs leave the effect indistinguishable from run-to-run variance.
- The candidate adds real risk for no demonstrated gain: the per-instance snapshot is same-scope — accounts mutated after the first successful read in a scoped lifetime are not visible to later reads of the cached key (matching existing repository-cache semantics, but enlarging them). That freshness cost is not justified by any measured benefit.
- Warm measurement was deliberately omitted on code-path grounds: a warm cache hit in `CachingDashboardQueryService.GetOverview` invokes only `DashboardQueryService.RefreshAssetsDistribution`, which reuses the cached `LiabilitiesPerType`/`LiabilitiesPerAccount` and never calls `ILiabilitiesService` — so a warm run would not exercise the changed code at all. This is a stated code-path exclusion, **not** a measured warm-equality result. No confirm pair was run because an inconclusive low-impact candidate does not warrant it; that limitation applies to these results (single-run, cold-only).
- Conclusion: **do not adopt**.

## Correctness and restoration

- Candidate suite (after parent's correction of the key-isolation test): **2257 passed** (baseline 2253 + 4 new cases), exit 0 — [`raw/e04-review-tests-retry.log`](./raw/e04-review-tests-retry.log). The first parent run ([`raw/e04-review-tests.log`](./raw/e04-review-tests.log)) had one failure: an unrelated bUnit `WaitForAssertion` timeout in `RangeRefreshFailureTests.ChangedOwner_DoesNotRetainPreviousUsersData` (AssetsTimeSeriesCardContainer case, check count 0), not a candidate-behaviour assertion; the single retry passed with no test edits.
- Restored suite: **2253 passed**, exit code 0 — [`raw/e04-restoration-tests.log`](./raw/e04-restoration-tests.log).

Restoration proof (test sub-agent): HEAD verified as `8f987e2e5f909716ece1d396f96c2701fbc3707a`; the worktree diff of exactly the two candidate paths was byte-identical to [`e04-candidate.patch`](./e04-candidate.patch) before restoration; the full pre-restoration worktree diff (all four modified files) is preserved in [evidence/e04-pre-restoration-worktree.patch](./evidence/e04-pre-restoration-worktree.patch); then `git restore --source=8f987e2e5f909716ece1d396f96c2701fbc3707a -- code/FinanceManager.Application/MoneyFlow/NetWorth/LiabilitiesService.cs code/FinanceManager.Tests.Unit/Application/Services/LiabilitiesServiceTests.cs` was executed. Post-restore: `git diff` for the two candidate paths is empty (0 lines); the preexisting modifications to `GuestAccountSeeder.cs` and `BenchmarkEnvironment.cs` are intact, as is untracked `docs/performance/`.

## Reproduction and evidence

Commands (see checkpoint docs for full environment):

```sh
# Benchmarks, from code/FinanceManager.Benchmarks (cold cache)
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=1 \
  dotnet run -c Release -- --filter '*Dashboard*' --artifacts ../../docs/performance/dashboard-experiments/iteration-01/raw/<run-dir>

# Unit tests, from code/ (MTP runner; must be run inside code/)
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj > ../docs/performance/dashboard-experiments/iteration-01/raw/<test-log>.log 2>&1

# Restore candidate files to HEAD
git restore --source=8f987e2e5f909716ece1d396f96c2701fbc3707a \
  -- code/FinanceManager.Application/MoneyFlow/NetWorth/LiabilitiesService.cs \
     code/FinanceManager.Tests.Unit/Application/Services/LiabilitiesServiceTests.cs
```

Environment: macOS (Apple M1 Max), .NET SDK 10.0.302 / runtime 10.0.10, BenchmarkDotNet 0.15.8, `InProcessEmitToolchain`, IterationCount=10 (time-based 250 ms), WarmupCount=3. Seed template SHA256: `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867` (unchanged). These are offline, server-side SQLite in-process API measurements — not browser paint/network results, and not PostgreSQL or SQL Server.

- [Baseline checkpoint](./e04-baseline.md)
- [Candidate cold checkpoint](./e04-candidate-cold.md)
- [Implementation record](./e04-implementation.md)
- [Review validation (2257 passed)](./e04-review-validation.md)
- [Candidate patch](./e04-candidate.patch)
- Raw: [baseline log](./raw/e04-baseline-cold.log), [candidate log](./raw/e04-candidate-cold.log), [candidate tests](./raw/e04-candidate-tests.log), [review failure (parent)](./raw/e04-review-tests.log), [review retry](./raw/e04-review-tests-retry.log), [restoration tests](./raw/e04-restoration-tests.log)
- Full JSON reports: [`raw/e04-baseline-cold/…report-full.json`](./raw/e04-baseline-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [`raw/e04-candidate-cold/…report-full.json`](./raw/e04-candidate-cold/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json)
- [Pre-restoration worktree evidence](./evidence/e04-pre-restoration-worktree.patch)
