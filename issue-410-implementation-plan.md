# Implementation Plan — Issue #410
## `StockEntryRepository` per-ISIN query loops and `GetNextYounger` account-isolation bug

---

## Context

**Repository:** `avresial/FinanceManager`
**Issue:** [#410](https://github.com/avresial/FinanceManager/issues/410)
**Branch to create:** `410-stockentryrepository-per-isin-query-loops`
**Base branch:** `develop`
**Size:** `size/M` (already labelled)

---

## Problem Summary

File: `code/FinanceManager.Infrastructure/Repositories/Account/Entry/StockEntryRepository.cs`

Two explicit interface implementations have bugs:

### Bug 1 — Correctness: Missing `AccountId` filter in `GetNextYounger` (line 180)

```csharp
// WRONG — scans the entire table for distinct ISINs
var isins = await context.StockEntries.Select(m => m.Isin).Distinct().ToListAsync();
```

It then fires per-ISIN queries filtered by `accountId`, but ISINs from *other* accounts are included in the loop — causing wasted round trips and, in theory, incorrect aggregation if a bug elsewhere removes the account filter from the inner query.

Compare with `GetNextOlder` (line 140-144) which correctly adds `.Where(e => e.AccountId == accountId)`.

### Bug 2 — Performance: N+1 query pattern in both dictionary-returning overloads (lines 136–159 and 177–195)

Both `GetNextOlder(int accountId, DateTime date)` and `GetNextYounger(int accountId, DateTime date)` that return `Dictionary<string, StockAccountEntry>`:

1. Query distinct ISINs — 1 round trip
2. For each ISIN, query the boundary entry — N additional round trips

`RecalculateValues` calls `GetNextOlder` on every entry add/update, making this the **hot write path**.

---

## Files to Change

| File | Action |
|------|--------|
| `code/FinanceManager.Infrastructure/Repositories/Account/Entry/StockEntryRepository.cs` | Fix both bugs |
| `code/FinanceManager.Tests.Unit/Infrastructure/Repositories/StockEntryRepositoryTests.cs` | **Create** — new unit test file |
| `CHANGELOG.md` | Add entry under `[Unreleased] > Fixed` |

No other files need to change. This is a pure infrastructure-layer fix.

---

## Step-by-Step Implementation

### Step 0 — Setup

```bash
# Verify .NET 10 SDK is available
dotnet --list-sdks   # must show 10.*

# If missing:
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0

# Create feature branch from develop
git fetch origin develop
git checkout -b 410-stockentryrepository-per-isin-query-loops origin/develop
```

---

### Step 1 — Fix `StockEntryRepository.cs`

**Target methods** (both are explicit interface implementations):

- Lines 136–159: `Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(...)`
- Lines 177–195: `Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(...)`

#### Replacement for `GetNextOlder` (lines 136–159)

Replace the ISIN-loop with a single query that fetches all candidate rows, then groups in memory:

```csharp
async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(int accountId, DateTime date)
{
    var candidates = await context.StockEntries
        .Where(e => e.AccountId == accountId && e.PostingDate < date)
        .OrderByDescending(e => e.PostingDate)
        .ThenByDescending(e => e.EntryId)
        .ToListAsync();

    return candidates
        .GroupBy(e => e.Isin)
        .ToDictionary(g => g.Key, g => g.First());
}
```

**Why this works:**
- Single DB round trip (one `WHERE AccountId = ? AND PostingDate < ?` query).
- Ordering by `PostingDate DESC, EntryId DESC` means the first entry per group is the "next older" boundary row.
- In-memory `GroupBy` + `First()` picks that boundary efficiently.
- Compatible with both the EF Core InMemory provider (tests) and relational providers.

#### Replacement for `GetNextYounger` (lines 177–195)

Same pattern, fix the missing account filter and eliminate the loop:

```csharp
async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(int accountId, DateTime date)
{
    var candidates = await context.StockEntries
        .Where(e => e.AccountId == accountId && e.PostingDate > date)
        .OrderBy(e => e.PostingDate)
        .ThenBy(e => e.EntryId)
        .ToListAsync();

    return candidates
        .GroupBy(e => e.Isin)
        .ToDictionary(g => g.Key, g => g.First());
}
```

**Key change from the original:** the `Where` clause now includes `e.AccountId == accountId` (was missing before), and the entire loop is gone.

---

### Step 2 — Create Unit Tests

Create the file `code/FinanceManager.Tests.Unit/Infrastructure/Repositories/StockEntryRepositoryTests.cs`.

Follow the exact same pattern used by `StockPriceRepositoryTests.cs`:
- Use `[Collection("Infrastructure")]` and `[Trait("Category", "Unit")]`
- Create a fresh in-memory `AppDbContext` per test via `UseInMemoryDatabase(Guid.NewGuid().ToString())`
- Instantiate `StockEntryRepository` directly (no mocking)
- Cast to the explicit interface `IStockAccountEntryRepository<StockAccountEntry>` where needed to call the dictionary-returning overloads

#### Helper factory methods to add at the top of the test class

```csharp
private static AppDbContext CreateContext()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;
    return new AppDbContext(options);
}

private static StockAccountEntry MakeEntry(int accountId, string isin, DateTime date, decimal valueChange = 10m) =>
    new(accountId, 0, date, 0m, valueChange, isin, InvestmentType.Equity) { Ticker = isin };
```

#### Required test cases

All tests use `var ct = TestContext.Current.CancellationToken;` for cancellation tokens.

---

##### `GetNextOlder` — dictionary overload

**Test 1: Returns correct boundary entry per ISIN**
```
Setup: account 1 has entries for AAPL and MSFT, both before the query date
Assert: result dictionary has both ISINs, each pointing to the most-recent entry before the date
```

**Test 2: Ignores entries from other accounts**
```
Setup: account 1 has AAPL entries; account 2 has GOOGL entries (same date range)
Call: GetNextOlder for accountId=1
Assert: result only contains "AAPL", not "GOOGL"
```
This is the critical regression test for the account-isolation invariant.

**Test 3: Returns empty dictionary when no entries are older than the date**
```
Setup: account 1 has only entries >= the query date
Call: GetNextOlder for that date
Assert: result is empty
```

**Test 4: Picks the most-recent entry when multiple exist for the same ISIN**
```
Setup: account 1 has three AAPL entries on Jan 1, Jan 5, Jan 10; query date = Jan 15
Assert: result["AAPL"].PostingDate == Jan 10
```

---

##### `GetNextYounger` — dictionary overload

**Test 5: Returns correct boundary entry per ISIN**
```
Setup: account 1 has entries for AAPL and MSFT, both after the query date
Assert: result dictionary has both ISINs, each pointing to the earliest entry after the date
```

**Test 6: Ignores entries from other accounts (account-isolation bug regression)**
```
Setup: account 1 has AAPL entries after the date; account 2 has TSLA entries after the same date
Call: GetNextYounger for accountId=1
Assert: result only contains "AAPL", not "TSLA"
```
This directly tests the line 180 bug where ISINs from all accounts were collected.

**Test 7: Returns empty dictionary when no entries are younger than the date**
```
Setup: account 1 has only entries <= the query date
Call: GetNextYounger
Assert: result is empty
```

**Test 8: Picks the earliest entry when multiple exist for the same ISIN**
```
Setup: account 1 has three AAPL entries on Jan 20, Jan 25, Jan 30; query date = Jan 15
Assert: result["AAPL"].PostingDate == Jan 20
```

---

### Step 3 — Build and Test

```bash
dotnet build ./code/FinanceManager.slnx
# Must produce 0 errors, 0 warnings

cd code
dotnet test --project ./FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj
# All tests must pass, including the 8 new ones
```

---

### Step 4 — Format

```bash
# From repo root
dotnet format ./code/FinanceManager.slnx
```

---

### Step 5 — Update CHANGELOG.md

Add under `[Unreleased]` → `### Fixed` (create the section if it doesn't exist yet):

```markdown
### Fixed

- `StockEntryRepository.GetNextYounger` no longer scans ISINs from all accounts — the missing `AccountId` filter is now applied before the ISIN query, preventing cross-account data leakage and wasted round trips #410
- Replace N+1 per-ISIN query loops in `StockEntryRepository.GetNextOlder` and `GetNextYounger` (dictionary overloads) with a single query + in-memory group, eliminating O(ISIN-count) round trips on the hot write path #410
```

Follow Keep-a-Changelog format; CalVer date `26.6.12` (or whatever today's date is when you run this).

---

### Step 6 — Commit, Push, PR

```bash
git add code/FinanceManager.Infrastructure/Repositories/Account/Entry/StockEntryRepository.cs \
        code/FinanceManager.Tests.Unit/Infrastructure/Repositories/StockEntryRepositoryTests.cs \
        CHANGELOG.md

git commit -m "Fix GetNextYounger account filter and N+1 ISIN loops #410"

git push -u origin 410-stockentryrepository-per-isin-query-loops
```

Open a **draft PR** against `develop` (never `main`). PR body must include `closes #410`.

---

## Key Constraints to Respect (from CLAUDE.md)

- **No new abstractions** — touch only the two methods; don't introduce helper classes.
- **Primary constructors** — the existing `StockEntryRepository(AppDbContext context)` primary constructor is correct, keep it.
- **Namespaces match folder paths** — new test file namespace: `FinanceManager.Tests.Unit.Infrastructure.Repositories`.
- **No `@inject` in Razor** — not applicable here (backend only).
- **`[Inject]` properties** — not applicable here.
- **Build is strict** — zero warnings.
- **Tests run from inside `code/`** — required due to `global.json` and the MTP runner.
- **No UI change** — no screenshots needed.
- **Include `#410`** in the commit message subject.

---

## What NOT to Change

- Do not touch `RecalculateValues` — it already operates per-ISIN correctly (it receives a single entryId, not a dictionary).
- Do not touch `GetNextOlder(int accountId, int entryId)` or `GetNextYounger(int accountId, int entryId)` — those single-entry overloads are already correct (no loop, no missing filter).
- Do not touch `GetNextOlder(int accountId, DateTime date)` or `GetNextYounger(int accountId, DateTime date)` returning `Task<StockAccountEntry?>` — those are also correct.
- Do not modify the domain interface `IStockAccountEntryRepository` — the signatures stay the same.
- Do not add EF Core migrations — this is a query-only change.

---

## Quick Diff Preview

```diff
-   async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(int accountId, DateTime date)
-   {
-       Dictionary<string, StockAccountEntry> result = [];
-
-       var isins = await context.StockEntries
-                               .Where(e => e.AccountId == accountId)
-                               .Select(m => m.Isin)
-                               .Distinct()
-                               .ToListAsync();
-
-       foreach (var isin in isins)
-       {
-           var nextOlder = await context.StockEntries
-                  .Where(e => e.Isin == isin && e.AccountId == accountId && e.PostingDate < date)
-                  .OrderByDescending(e => e.PostingDate)
-                  .FirstOrDefaultAsync();
-
-           if (nextOlder is null) continue;
-
-           result.Add(isin, nextOlder);
-       }
-
-       return result;
-   }
+   async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(int accountId, DateTime date)
+   {
+       var candidates = await context.StockEntries
+           .Where(e => e.AccountId == accountId && e.PostingDate < date)
+           .OrderByDescending(e => e.PostingDate)
+           .ThenByDescending(e => e.EntryId)
+           .ToListAsync();
+
+       return candidates
+           .GroupBy(e => e.Isin)
+           .ToDictionary(g => g.Key, g => g.First());
+   }

-   async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(int accountId, DateTime date)
-   {
-       Dictionary<string, StockAccountEntry> result = [];
-       var isins = await context.StockEntries.Select(m => m.Isin).Distinct().ToListAsync(); // BUG: no accountId filter
-
-       foreach (var isin in isins)
-       {
-           var nextOlder = await context.StockEntries
-                  .Where(e => e.Isin == isin && e.AccountId == accountId && e.PostingDate > date)
-                  .OrderBy(e => e.PostingDate)
-                  .FirstOrDefaultAsync();
-
-           if (nextOlder is null) continue;
-
-           result.Add(isin, nextOlder);
-       }
-
-       return result;
-   }
+   async Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(int accountId, DateTime date)
+   {
+       var candidates = await context.StockEntries
+           .Where(e => e.AccountId == accountId && e.PostingDate > date)
+           .OrderBy(e => e.PostingDate)
+           .ThenBy(e => e.EntryId)
+           .ToListAsync();
+
+       return candidates
+           .GroupBy(e => e.Isin)
+           .ToDictionary(g => g.Key, g => g.First());
+   }
```
