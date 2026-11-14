# E05 candidate — hoist `GetStoredBondsIds()` out of the daily loop

Status: implemented and validated. **STOP before benchmarking** (per plan). No commits, no reseed, no restore.

## Changes (exact two-file scope)

### 1. `code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs` (range overload only)

- Added one local just before the daily loop:
  ```csharp
  var bondAccountsWithIds = bondAccounts.Select(account => (Account: account, DetailsIds: account.GetStoredBondsIds())).ToArray();
  ```
  Accounts are not mutated inside the loop, so computing each account's stored bond ids once is safe.
- Replaced only the bond iteration inside the daily loop with `foreach (var (account, detailsIds) in bondAccountsWithIds)` and an inner `foreach` over `detailsIds`.

Untouched: point overload (`GetNetWorth(int, Currency, DateTime)`), `BondDashboardContext`, `LoadReferencedDetailsAsync` call, `GetThisOrNextOlder`, `GetOrComputePrice`, descending date order (`for (date = end; date >= start; ...AddDays(-1))`), `Math.Round(..., 2)`, all repository calls. No cache or new abstraction introduced. `GetStoredBondsIds()` itself is unchanged, so its ordering (entries' ids first, then boundary keys, distinct) and boundary-key handling are preserved.

### 2. `code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs`

Appended one regression test: `GetNetWorth_OverRange_BondAccounts_ValueEachInstrumentOncePerAccount`, plus a private `CreateZeroRateDetails(int id)` fixture copied (minimally) from the `BondDashboardContextTests.CreateDetails` helper.

## Numeric fixture explanation

- Range fixed at **2024-01-01 .. 2024-01-03** (UTC midnight), `userId = 1`. Accounts are built with direct `BondAccount` constructors passing `entries`/`nextOlderEntries`, so no `Add()` value recalculation interferes; entry `Value`s are the values used for pricing.
- Bond definitions: zero-rate `BondCalculationMethod` (`Rate = 0`), `UnitValue = 100m`, active window **2023-01-01 .. 2025-01-01**, PLN, `InflationBond`. With rate 0, `BondAccountEntry.GetPriceAt` (verified in source) returns exactly `Value * UnitValue` for any date on/after the posting date, so expected values are hand-computable without simulating interest accrual.
- Fixture layout:
  - **Account A (`Bonds A`, id 1)** — instrument 1 present both in-range and at the boundary (dedup case): entries posted 2024-01-01 (Value 10) and 2024-01-03 (Value 15), plus `NextOlderEntries[1]` posted 2023-12-20 (Value 5). Instrument 2 is **boundary-only**: `NextOlderEntries[2]` posted 2023-12-28 (Value 4).
  - **Account B (`Bonds B`, id 2)** — shares instrument 1 from its own entry (account identity): one entry posted 2023-12-15 (Value 7), selected on every day of the range.
  - **Account C (`Bonds C`, id 3)** — empty account; contributes nothing and exercises the zero-id path.
- Expected daily values (price = Value × 100):
  | Day   | A: instrument 1 selection      | A: boundary-only instr. 2 | B: shared instr. 1 | Total    |
  |-------|--------------------------------|---------------------------|--------------------|----------|
  | Jan 1 | entry posted that day (10) → 1000 | 4 × 100 = 400          | 7 × 100 = 700      | **2100** |
  | Jan 2 | next-older carry-over (10) → 1000 | 400                     | 700                | **2100** |
  | Jan 3 | entry posted that day (15) → 1500 | 400                     | 700                | **2600** |
- What the assertions verify: no duplicate counting of instrument 1 in Account A (an undeduplicated id list would add the boundary Value-5 entry, making day 1 = 2600 instead of 2100); the boundary-only instrument contributes exactly once per account; the shared instrument is priced independently per account (day 3 counts it twice: 1500 from A + 700 from B); multiple in-range entries exercise same-day vs next-older selection; result has exactly **3** keys in descending date order `[Jan 3, Jan 2, Jan 1]`. No timing assertions. The test does not compare the optimized algorithm to itself — all expected values are computed independently from the fixture definition.

## Commands and results

| Step | Command (run as given) | Exit code |
|------|------------------------|-----------|
| Format (from repo root) | `dotnet format ./code/FinanceManager.slnx --include code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs code/FinanceManager.Tests.Unit/Application/Services/NetWorthServiceTests.cs` | **0** |
| Tests (from `code/`) | `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj > ../docs/performance/dashboard-experiments/iteration-01/raw/e05-candidate-tests.log 2>&1` | **0** (captured explicitly via `echo $?`, not inferred) |

Test totals from the log (`raw/e05-candidate-tests.log`): **total: 2254, failed: 0, succeeded: 2254, skipped: 0** — matches the expected 2254. Log saved at `docs/performance/dashboard-experiments/iteration-01/raw/e05-candidate-tests.log`.

## Artifacts

- Patch (exact two-file diff): `docs/performance/dashboard-experiments/iteration-01/e05-candidate.patch`
- Test log: `docs/performance/dashboard-experiments/iteration-01/raw/e05-candidate-tests.log`

## Limitations

- Validation is unit-level only; no benchmark was run (STOP before benchmarking per plan). The perf win is structural (one id-list construction per account instead of one per day) but unmeasured until E06.
- The regression test covers the bond branch of the range overload with zero-rate definitions; it does not exercise non-zero rates, currency conversion, or investment-series interaction beyond the existing tests in the file.
- Pre-existing working-tree modifications to `GuestAccountSeeder.cs` and `BenchmarkEnvironment.cs` (and untracked `docs/performance/`) were present at HEAD 8f987e2e before this work started and were preserved untouched; they are not part of this change set.
