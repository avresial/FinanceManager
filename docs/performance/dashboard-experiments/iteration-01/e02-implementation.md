# E02 implementation — candidate for parent review

Status: implemented, formatted, unit-tested. STOPPED before benchmarking as instructed. No commits, no rollback, no other optimizations, no subagents. Preexisting `GuestAccountSeeder.cs` / `BenchmarkEnvironment.cs` changes and all docs preserved untouched (diff scoped to the two files below). Fresh E02 warm/cold baselines retained in `raw/`.

## Exact edits

### 1. `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs` — range overload only

- `GetAccounts<CurrencyAccount>(userId, start, end)` → `GetAccounts<CurrencyAccount>(userId, start, end, includeEntryMetadata: false)`.
- `GetAccounts<BondAccount>(userId, start, end)` → `GetAccounts<BondAccount>(userId, start, end, includeEntryMetadata: false)`.
- Two production call changes only (plus a short comment noting the cache-key tradeoff). The single-date overload and all `InvestmentAccount` calls are unchanged. No caching or query changes anywhere else; `GetValueRange` row selection, ordering, and core fields are preserved — only label Includes are omitted.

### 2. `code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs`

- Constructor default currency/bond mocks now pass `It.IsAny<bool>()` so both overload modes (range → `false`, single-date → default `true`) resolve to empty streams. Investment mock unchanged.
- Range-specific currency/bond setups in `GetNetWorth_OverRange_FetchesAllInvestmentAccountsInOneBatchedCall` now match with explicit `false`. Point-test setup keeps the default (`true`).
- Added regression test `GetNetWorth_OverRange_FetchesCurrencyAndBondsInMetadataFreeMode_AndKeepsValues`: range 2024-01-01..2024-03, user 1; false-mode currency account with entries Jan 1 balance 100 / Jan 2 balance 150 (built directly from entry lists — no `Add` recalculation); false-mode bond account with one Jan 1 entry of 10 units on details id 1, definition unit value 100 at zero rate (`UntilDate` 2025-01-01), returned via the existing repository mock; constructors as in `BondDashboardContextTests`. Asserts daily net worth 1100 / 1150 / 1150, and verifies currency + bond `GetAccounts(1, start, end, false)` exactly once each with `(…, true)` never for that range. Immutable past dates; no timing asserts.

## Validation

- From repo root: `dotnet format ./code/FinanceManager.slnx --include code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs` — completed, no changes reported (exit 0).
- From `code/`: `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj > ../docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-tests.log 2>&1`
  - Exit code: **0**. Log summary: `Test run summary: Passed!`, total **2254** (preexisting 2253 + 1 new), failed 0, skipped 0. No other NetWorth-related mock setups required changes — no broad rewrites were needed or made.

## Artifacts

- Candidate patch (diff of exactly the two files, post-format): `docs/performance/dashboard-experiments/iteration-01/e02-candidate.patch`
- Test log: `docs/performance/dashboard-experiments/iteration-01/raw/e02-candidate-tests.log`

## Limitations / invariants for parent inspection

- **Cache-key tradeoff**: the AccountRepository cache key includes `includeEntryMetadata`, so the new false-mode range queries are distinct cache entries from any prior true-mode lookups. Actual dashboard savings must be measured against the retained E02 warm/cold baselines — not assumed.
- Row contents: rows/order/core fields unchanged; only label metadata is omitted, so monetary values are unaffected (covered by the parity regression test).
- Single-date `GetNetWorth` still requests full metadata (`includeEntryMetadata` default `true`) — no behavior change there.
