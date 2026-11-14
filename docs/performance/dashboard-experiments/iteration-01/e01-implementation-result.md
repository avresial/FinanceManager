# E01 implementation result — candidate for parent review

Status: implemented, formatted, unit-tested. STOPPED for parent code review before benchmarking or restoration. No commits, no benchmarks, no rollback, no other experiments touched. Preexisting GuestAccountSeeder/BenchmarkEnvironment changes untouched (diff scoped to the three files below).

## Exact edits

### 1. `code/FinanceManager.Application/FinancialAccounts/Bond/Valuation/BondDashboardContext.cs`

- Added request-local field:
  `Dictionary<(int AccountId, int EntryId, int BondDetailsId, DateOnly PostingDate, decimal Value), (DateOnly Through, Dictionary<DateOnly, decimal> Prices)> _histories`.
  `_prices` and all existing fallback behavior retained unchanged. No shared/static cache.
- Added public `PreparePriceHistory(int accountId, BondAccountEntry entry, DateOnly through)`:
  - builds the five-part key `(accountId, entry.EntryId, entry.BondDetailsId, DateOnly.FromDateTime(entry.PostingDate), entry.Value)`;
  - returns immediately if an existing history covers the horizon (`existing.Through >= through`);
  - skips preparation (stores nothing) when `through` precedes the posting date, preserving pre-posting behavior via the fallback path;
  - otherwise stores `(through, entry.GetPrice(through, GetDetails(entry.BondDetailsId)))`. Missing definitions still throw here, where pricing actually needs them.
- Modified `GetOrComputePrice`:
  - existing exact-date `_prices` cache lookup retained FIRST (unchanged);
  - before the unchanged `entry.GetPriceAt` fallback, looks up the five-part history key; if a history exists and `date <= Through`, returns `history.Prices.GetValueOrDefault(date)` — zero for absent/gap dates and pre-posting dates, exactly matching `GetPriceAt`;
  - otherwise executes the unchanged fallback (compute + memoize in `_prices`);
  - never auto-extends history. Documented that callers explicitly prepare a bounded series once; this prevents ascending lookups from rebuilding the prefix.

### 2. `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs` (range overload only)

- Computed `var bondHistoryEnd = DateOnly.FromDateTime(end);` exactly once, before the daily loop (after the investment series batch call).
- In the bond loop, after `if (entry is null) continue;`, inserted `bondDashboardContext.PreparePriceHistory(account.AccountId, entry, bondHistoryEnd);` immediately before the existing `GetOrComputePrice` call. Lazy preparation: only entries actually selected by the existing date/tie logic are evaluated; all days of an unchanged snapshot use the same fixed horizon → one build per selected snapshot.
- Point overload (`GetNetWorth(userId, currency, DateTime)`) unchanged. `GetStoredBondsIds`, query shape, ordering, and unrelated loops untouched.

### 3. `code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs`

Baseline tests retained verbatim. Added (existing helpers only, no timing asserts):

- `GetOrComputePrice_PreparedHistory_MatchesEntryGetPriceAt_ForAscendingDescendingAndShuffledDates` — prepared-path version of the existing theory; prepares through day 730 once before accesses; asserts every result equals `entry.GetPriceAt`.
- `GetOrComputePrice_PreparedHistory_MatchesEntryGetPriceAt_AroundMethodExpiryGap` — prepared-path version of the expiry fact; prepares through day 10 before accesses (offsets 0/4/5/6/10); asserts parity.
- `GetOrComputePrice_PreparedHistory_UsesFreshValueAfterEntryMutation` — prepares at Value=10, mutates Value=12: lookup equals fresh `GetPriceAt` without re-preparation (stale history keyed by old Value is bypassed); then prepares the changed snapshot and asserts parity.
- `GetOrComputePrice_PreparedHistory_FallsBackBeyondHorizon_ThenMatchesAfterExplicitExtension` — prepares through day 366, requests day 730 (unchanged fallback), then explicitly prepares through day 730 and asserts parity for days 365/366/730.

## Validation

- `dotnet format ./code/FinanceManager.slnx --include <the three files>` from repository root: completed, no changes reported (exit 0).
- From `code/`: `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj` with stdout/stderr redirected to `docs/performance/dashboard-experiments/iteration-01/raw/e01-implementation-tests.log`.
  - Exit code: **0**. Log summary line: `Test run summary: Passed!` (no failures).

## Artifacts

- Candidate patch (diff of exactly the three files, post-format): `docs/performance/dashboard-experiments/iteration-01/e01-candidate.patch`
- Test log: `docs/performance/dashboard-experiments/iteration-01/raw/e01-implementation-tests.log`

## Limitations / invariants for parent inspection

- Explicit successive preparations to increasing horizons rebuild the series (each larger horizon re-runs `entry.GetPrice`). The dashboard caller fixes the maximum once (`bondHistoryEnd = end`), so this does not occur on that path; ordinary ascending lookups never extend history automatically — out-of-horizon dates fall back to the unchanged per-date computation.
- History is keyed by the entry snapshot (including `Value` and posting date); a mutated entry bypasses its stale history via fallback, matching the existing `_prices` staleness semantics.
- Pre-posting lookups are never prepared; they keep the exact baseline behavior (`GetPriceAt` → 0).
