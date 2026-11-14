# E04 implementation — candidate for parent review

Status: implemented, formatted, unit-tested. STOPPED before benchmarking as instructed. No commits, no restore/reseed, no subagents. Preexisting `GuestAccountSeeder.cs` / `BenchmarkEnvironment.cs` changes and all docs preserved untouched (diff scoped to the two files below).

## Exact edits

### 1. `code/FinanceManager.Application/MoneyFlow/NetWorth/LiabilitiesService.cs`

- Added instance-local state: `private readonly Dictionary<(int UserId, DateTime End), List<CurrencyAccount>> _endAccounts = [];` with a comment noting it is a same-scope read snapshot and that the repository already deduplicates identical `(end, end)` queries — this change removes repeated async enumeration only; **no claim of eliminating DB access**.
- Added small helper `private async Task<List<CurrencyAccount>> GetEndAccountsAsync(int userId, DateTime end)`: on cache miss it fully enumerates the EXACT existing call `financialAccountService.GetAccounts<CurrencyAccount>(userId, end, end)` into a `List<CurrencyAccount>` and stores the list **only after successful enumeration** (a failed enumeration leaves nothing cached). Cache key is the exact `(UserId, End)` value — no date truncation. The `start` parameter remains intentionally unused as before; the `includeEntryMetadata` flag is unchanged (default `true`).
- `GetEndLiabilitiesPerAccount`: `await foreach (... GetAccounts<CurrencyAccount>(userId, end, end))` → `foreach (... in await GetEndAccountsAsync(userId, end))`. Selection/filter/output body unchanged.
- `GetEndLiabilitiesPerType`: `await foreach (... GetAccounts<...>(...).GroupBy(x => x.AccountType))` → `foreach (... in (await GetEndAccountsAsync(userId, end)).GroupBy(x => x.AccountType))`. Body unchanged; grouping is over the complete list so group order remains first-occurrence order of the full input (positive-only accounts still fix their group's position), with no prefilter before grouping.
- `IsAnyAccountWithLiabilities` and `GetLiabilitiesTimeSeries` untouched.

### 2. `code/FinanceManager.Tests.Unit/Application/Services/LiabilitiesServiceTests.cs`

All four existing tests preserved verbatim. Appended (plus one private static helper):

- `CreateEndFixtureAccounts()`: fixed user 1, end 2024-01-03; ordered accounts built with the entries constructor (explicit negative/zero values, no `Add` recalculation): id1 Loan +100 (positive-only), id2 Other −20, id3 Loan −50, id4 Loan zero, id5 Other empty entries with older `NextOlderEntry` −30, id6 Loan empty without boundary.
- Theory `EndLiabilities_BothCallOrders_ReturnSameRows_AndSingleRepositoryCall(bool perTypeFirst)`: both call orders on one service instance hit the same key; asserts account rows (ids 2,3,4,5; values −20,−50,0,−30) and type totals/order (Loan −50 then Other −50 — Loan appeared first via positive id1); verifies exactly **one** repository call for `(1, end, end)` with `includeEntryMetadata == true` (`It.Is<bool>` matcher). A distinct `start` value is passed to prove it remains unused.
- Fact `EndLiabilities_DifferentUserOrExactEnd_AreReadSeparately`: fresh service per key; key isolation by user (user 2) and exact end +1 minute on the same calendar day; each mock set up only for its exact arguments, so success proves no date truncation or cross-key reads.
- Fact `EndLiabilities_FailedEnumeration_IsNotCached_RetryEnumeratesAgain`: first repository enumeration throws (`InvalidOperationException`); the failed read is not cached — retry re-enumerates and returns full rows; verifies exactly two repository calls for the same key.
- Moq: typed locals for expression collections, exact argument values with `It.Is<bool>` matchers; existing `.ToListAsync(cancellationToken:)` pattern used throughout (`.AsTask()` where a `Task` is required).

## Validation

- From `code/`: `dotnet format ./FinanceManager.slnx --include <each of the two paths>` — exit 0, no changes reported.
- From `code/`: `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj > ../docs/performance/dashboard-experiments/iteration-01/raw/e04-candidate-tests.log 2>&1`
  - Exit code: **0**. Log summary: `Test run summary: Passed!`, total **2257** (baseline 2253 + 4 new test cases), failed 0, skipped 0.

## Artifacts

- Candidate patch (diff of exactly the two files, post-format): `docs/performance/dashboard-experiments/iteration-01/e04-candidate.patch`
- Test log: `docs/performance/dashboard-experiments/iteration-01/raw/e04-candidate-tests.log`

## Scope limitations / invariants for parent inspection

- **No DB-query elimination claim**: `AccountRepository` already caches identical `(end, end)` reads; this change only removes repeated async enumeration and uses synchronous grouping on a shared per-instance list. Actual dashboard savings must be measured against baselines — not assumed.
- **Same-scope mutation freshness**: the cached list is a snapshot taken at first successful read within one scoped service instance (service is scoped, `MoneyFlow/Registration.cs`). Accounts mutated after that first read are not visible to later reads in the same scope; this matches the existing repository-cache semantics for identical queries.
- Cache is per-instance and keyed on exact `(UserId, DateTime End)` — no date truncation, so minute-level end differences remain distinct keys (covered by the isolation test).

## Parent review correction
The original isolation test used fresh instances and changed both key dimensions together, so it did not establish cache-key isolation. Parent replaced it with one service queried for user 1/end, user 2/end, and user 1/end+1 minute, asserting distinct values and exact read counts. A separate fresh instance then reads the identical key again. Validation logs: raw/e04-review-format.log and raw/e04-review-tests.log.
