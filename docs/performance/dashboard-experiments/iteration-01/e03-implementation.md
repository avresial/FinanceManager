# E03 candidate — implementation notes

**Status: implementation + tests complete, STOP before benchmarks (per parent instruction).**

## Change scope (two files only)

- `code/FinanceManager.Application/FinancialAccounts/Investments/Assets/AssetsServiceInvestment.cs`
  - Added an instance-level read-composition cache `_endValuations`, keyed by the immutable scalar tuple
    `(int UserId, int CurrencyId, string CurrencyCode, string CurrencySymbol, DateTime AsOf)` — scalars are
    snapshotted from the mutable `Currency` fields at lookup time; the `Currency` object itself is never used as key material.
  - Added private helper `GetEndValuationAsync(userId, currency, asOf)`: cache hit returns the stored tuple; a miss
    materializes accounts via the existing `financialAccountRepository.GetAccounts<InvestmentAccount>(userId, asOf.AddMinutes(-1), asOf)`
    call, values them with the existing batched `valuationService.GetAccountValueAsync(ids, currency, asOf)`, snapshots the
    returned map with `.ToDictionary()`, and stores the tuple **only after a successful valuation**. Sequential awaits on the shared
    DbContext; no statics, HybridCache, locks, Task caches, or new service interfaces.
  - `GetEndAssetsPerAccount` / `GetEndAssetsPerType` now use the helper instead of their duplicated load+valuation blocks. Per-account
    behavior is unchanged (original account ordering/name/AccountId; only matched value > 0 yielded). Per-type sums **all** dictionary
    values, negatives included, and yields Stock only when the total > 0 — never derived from positive-filtered rows.
  - All other service methods are untouched.

- `code/FinanceManager.Tests.Unit/Application/Services/AssetsServiceInvestmentTests.cs` (append-only)
  1. Theory (type-first vs account-first): accounts 7/8 valued 100/-20 with a deliberately shared name; both distributions on the same
     fixed key (user 1, PLN, 2024-01-03). Asserts Stock total 80, single account row id 7/value 100/name, one batched valuation and one
     repository read. Moq collection predicates use `It.Is`, not collection expressions.
  2. Fresh service instance revalues the identical key: first instance returns 100; after changing the valuation setup to 200 a new
     instance returns 200; total batched valuation calls = 2 (scope freshness).
  3. Cache-key isolation: same service instance called with different user, different currency, and different exact date (same day,
     +1 minute); non-empty accounts mocked for all reads; asserts four distinct returned values and exactly 4 valuation calls
     (one per repository read), proving no Date-only truncation or cross-key reuse.
  4. Failed valuation is not cached: first batched call throws `InvalidOperationException`, retry on the same key succeeds with value 100,
     total valuation calls = 2; no timing assertions.

## Verification

- Format: `dotnet format ./code/FinanceManager.slnx --include <the two paths>` — idempotent final state (second run produced no changes).
- Tests (exact mandated command from `code/`):
  `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj > ../docs/performance/dashboard-experiments/iteration-01/raw/e03-candidate-tests.log 2>&1`
  - **Exit code: 0** — `Passed! total: 2258, failed: 0, succeeded: 2258, skipped: 0` (baseline 2253 + 5 new cases).
- Two test bugs were caught and fixed by the suite itself before final green (both in my appended tests, not the production code): a missing
  `GetAccounts` mock for the `(user 1, nextMinute)` key, and an over-strict `Times.Once` expectation where user 1 is legitimately read once
  per distinct currency (corrected to `Times.Exactly(2)`).

## Limitations / notes

- The cache lifetime equals the service instance (scoped DI). Same-scope writes are not invalidated by these read-only paths — per the design,
  that invalidation is out of scope for E03.
- Parent review correction: `DateTime` has no five-integer year/month/day/hour/minute constructor; this is not an SDK or environment defect. The test uses the valid `new DateTime(2024, 1, 3).AddHours(9).AddMinutes(15)` expression. Parent also corrected the cache field name to `_endValuations` per CLAUDE.md; revalidation follows before measurement.
- No benchmarks run (per instruction); no commits made; preexisting GuestAccountSeeder/BenchmarkEnvironment working-tree changes and all docs preserved.

## Artifacts

- Exact two-file diff: `docs/performance/dashboard-experiments/iteration-01/e03-candidate.patch`
- Test log: `docs/performance/dashboard-experiments/iteration-01/raw/e03-candidate-tests.log`
