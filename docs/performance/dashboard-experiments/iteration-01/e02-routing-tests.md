# E02 routing tests checkpoint

Test-only checkpoint validating the `GetAccounts` range-method routing branch in
`AccountRepository` (metadata vs value projection), added before any candidate changes.

## Changes

Single file edited:
`code/FinanceManager.Tests.Unit/Infrastructure/Features/FinancialAccounts/Shared/Repositories/AccountRepositoryTests.cs`

- Existing test `GetAccounts_ReusesIdenticalRangeUntilAWrite` retained unchanged.
- Added two `[Theory]` tests, each with `[InlineData(true)]` / `[InlineData(false)]`:
  - `GetAccounts_Currency_RoutesToMetadataRangeOrValueRange(bool includeEntryMetadata)`
  - `GetAccounts_Bond_RoutesToMetadataRangeOrValueRange(bool includeEntryMetadata)`

Each test:

- Real fixture identity: `userId = 7`, single account `accountId = 11`; range
  `start = 2024-01-01`, `end = 2024-02-01`.
- Nonempty `GetAll` result (one real account), so the empty-account early return is
  not taken and the routing branch is actually exercised.
- Two in-range entries per account: Jan 5 (`EntryId 1`, `Value 100m`, `ValueChange 100m`)
  and Jan 10 (`EntryId 2`, `Value 150m`, `ValueChange 50m`); bond entries carry
  `BondDetailsId = 301`. Bond fixtures use only supported properties.
- Both `GetRange` and `GetValueRange` mocked to return the same core fields via typed
  list locals; the currency metadata variant additionally carries `Description`.
  Moq collection matchers use `It.IsAny<IReadOnlyCollection<int>>()` / `It.IsAny<DateTime>()`
  only (no collection expressions inside expression trees); unused repositories are
  `Mock.Of<T>()` with parentheses.
- Boundary mocks return explicitly typed empty dictionaries: currency
  `Dictionary<int, CurrencyAccountEntry>` via `GetNextOlder`/`GetNextYounger`; bond
  `Dictionary<int, Dictionary<int, BondAccountEntry>>` via
  `GetNextOlderPerInstrument`/`GetNextYoungerPerInstrument`.
- Assertions per theory case: exactly one account with `AccountId == 11`; both returned
  entries' core fields (`EntryId`, `PostingDate`, `Value`, `ValueChange`; plus bond
  `BondDetailsId`) match the fixture.
- Verification per theory case: selected range method called once, other range method
  never; account `GetAll(7)` called exactly once. The same request is then enumerated a
  second time and no extra selected-method call occurs (read-cache reuse). Concrete
  dates are used in verification matchers; `It.IsAny` is not used as the expected
  invocation value for the concrete arguments.

## Result

Command (run from `code/`):

```
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj > ../docs/performance/dashboard-experiments/iteration-01/raw/e02-routing-tests.log 2>&1
```

- Exit code: **0**
- Total tests: **2253** (baseline 2249 + 4 new theory cases)
- Passed: **2253**, failed: **0**, skipped: **0** (duration ~27.5 s)
- Log: `docs/performance/dashboard-experiments/iteration-01/raw/e02-routing-tests.log`

Working tree preserved: uncommitted `GuestAccountSeeder.cs` / `BenchmarkEnvironment.cs`,
untracked `BondHistoryBenchmarks.cs`, and `docs/performance/` untouched; no production
code, commits, benchmark runs, or other experiments performed.

## Limitations

- These tests prove mock-level routing parity: for each `includeEntryMetadata` value the
  correct one of `GetRange` vs `GetValueRange` is selected (and not re-invoked on cached
  re-enumeration) for both account types, and entry core fields flow through unchanged.
- They do **not** prove EF projection correctness: `GetValueRange`/`GetRange` are mocked to
  return identical fixture data; the real SQL projections, their performance characteristics,
  and any divergence in returned metadata remain unvalidated at this checkpoint.
