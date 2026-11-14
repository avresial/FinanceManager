# E04 review — unit-suite revalidation (test sub-agent)

Single full unit-suite revalidation after the parent's correction of the E04 key-isolation test. No benchmark, no edits, no commits, no restore/reseed, no concurrent builds/tests/benchmarks.

## Original failure (parent run)

Log: `raw/e04-review-tests.log` — exit code **2**, summary `Failed!`, total 2257, failed **1**, succeeded **2256**, skipped 0.

The only failing test:

```
failed (canceled) FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.RangeRefreshFailureTests.ChangedOwner_DoesNotRetainPreviousUsersData(card: typeof(...AssetsTimeSeriesCardContainer), valueSelector: ".fm-tsvc-value") (2s 101ms)
  Xunit.MicrosoftTestingPlatform.XunitException: Bunit.Extensions.WaitForHelpers.WaitForFailedException : The assertion did not pass within the timeout period.
    Check count: 0.
    Component render count: 1.
    Total render count across all components: 0.
```

i.e. a bUnit `WaitForAssertion` timeout in the `AssetsTimeSeriesCardContainer` theory case (check count 0), not an assertion failure about candidate behaviour. Not assumed flaky; no test edits made by this sub-agent.

## Retry run (this validation)

Command, executed exactly once from `code/`:

```
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj > ../docs/performance/dashboard-experiments/iteration-01/raw/e04-review-tests-retry.log 2>&1
```

Observed result (from `raw/e04-review-tests-retry.log`):

- Exit code: **0** (shell reported `EXIT=0`).
- Summary: `Test run summary: Passed!`, total **2257**, failed **0**, succeeded **2257**, skipped 0, duration 21s 093ms.

## Verdict

Pass observed on retry: exit 0 with 2257/2257 passed (includes the fixed E04 key-isolation test and all liability tests). The previously failing `RangeRefreshFailureTests.ChangedOwner_DoesNotRetainPreviousUsersData` case did not recur in this run. Parent decides measurement after review.

## Artifacts

- Retry log: `raw/e04-review-tests-retry.log`
- Original failure log (parent): `raw/e04-review-tests.log`

Worktree state preserved: the four modified sources (`GuestAccountSeeder.cs`, `LiabilitiesService.cs`, `BenchmarkEnvironment.cs`, `LiabilitiesServiceTests.cs`) and all docs untouched by this run.
