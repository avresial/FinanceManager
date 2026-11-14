# E01 recovery: checkpoint 1

The previous Qwen run was cancelled after repeated reads and diff preservation without experiment edits or benchmark results. Existing seeder/benchmark-environment edits and all evidence files are preserved. Provider/context settings are unchanged; cause of cycling is unproven.

## Parent design decision

A history cache rebuilt from posting date whenever the requested maximum increases remains quadratic for ascending daily requests. Do not implement that algorithm. Candidate design for the later implementation checkpoint: explicitly prepare each relevant entry history to the known request end date once, then use dictionary lookups for the daily loop. Keep the existing point-price method as fallback for unprepared calls; only claim acceleration for prepared series. Preparation must key on the same account/entry/definition/posting-date/value snapshot identity, preserve missing-day zero results, and avoid invalidating fresh definitions across requests. Ascending, descending and shuffled access must return identical values. Parent will inspect callers before fixing the exact implementation patch.

## Assigned now: correctness tests only

Edit only code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs. Reuse existing mock repository/setup patterns. Add a theory for ascending, descending and shuffled dates over days -1, 0, 1, 364, 365, 366, 730 from 2023-01-01; use nonzero 0.0365 rate and unitValue 100, with definition active until 2027-01-01. For each order assert context.GetOrComputePrice equals entry.GetPriceAt at every date; repeat the sequence. Add a separate gap/expiry parity case whose sole method expires 2023-01-05 and compare days 0, 4, 5, 6, 10 with GetPriceAt. These are semantic guards, not proof of linear complexity or performance.

Run from code/: dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj. Save stdout/stderr and exit code to docs/performance/dashboard-experiments/iteration-01/raw/e01-checkpoint-01-tests.log. Report diff and result in e01-checkpoint-01-result.md. No production edits, benchmarks, commits or additional experiments at this checkpoint. Parent reviews this checkpoint before baseline benchmark/implementation tasks.

## Bounded stop conditions

Read each required file once; no repeated status/diff backups. First deliverable is the test-file edit, within six tool calls after required guidance. At ten tool calls or ten minutes, stop and report actual artifacts and blockers even if incomplete. After a command fails twice, stop with its exact error rather than expanding scope. Do not start another discovery pass. Parent checks file diff and logs on return and will not treat tool activity as progress.
