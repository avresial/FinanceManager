# Operation-local dashboard asset distributions — #1026

Measured 2026-11-01 on Apple M1 Max, macOS 26.6.2, .NET SDK 10.0.302/runtime 10.0.10, BenchmarkDotNet 0.15.8. Baseline: `d7f107a9fcc853d2adc8f6eea043adb51e2d0420`. Candidate: the five production files identified by [source hashes](raw/candidate-source.sha256), shipped with this report.

## Result and scope

One bounded warm-cache SQLite API pair measured lower managed allocation for both overview windows. Both runs reused the same six-month seed template (`be0ef70aa29e7571e38d9493e5553edce2fd783c954db59a46486ef4758c61fa`); all four before/after seed hashes match. Both endpoint-validation runs completed all 48 calls successfully. The initial exploratory baseline overlapped compilation and is excluded; its artifacts remain in `/tmp/fm-1026-evidence/baseline`. The reported baseline replacement and candidate ran without concurrent builds or tests in this checkout.

| Window | Baseline B/op | Candidate B/op | Reduction | Baseline mean ms | Candidate mean ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Six months | 670,661 | 625,494 | 45,167 (6.73%) | 16.216 | 16.143 |
| One month | 640,940 | 596,263 | 44,677 (6.97%) | 15.643 | 14.430 |

Timing is descriptive for this single pair. There is no replicated latency, browser, production database, throughput or perceived-speed claim. API hosting is in-process with offline providers; measurement includes serialization and SQLite work, excludes sockets/TLS/browser rendering, and measures no concurrent workload. The host could not raise process priority on macOS (reported by both runs). BenchmarkDotNet's within-run error bars do not establish run-level replication. Its `Ratio` columns compare windows within a run, not the candidate against the baseline.

## Implementation and correctness

The existing `IAssetsService` contract now offers a paired read with a sequential default composition for unchanged typed services. `AssetsService` appends each typed result in registration order. Investments override the paired read and load account identities/names and batch valuation once, then project both distributions from those operation-local inputs. No service cache, DI registration, database schema, provider policy, public endpoint or overview DTO changes.

Negative investment values contribute to the type sum (the type row still requires a positive total). Account rows retain positive-only filtering, repository order, duplicate-name identity and account IDs. Scalar identity/name copies prevent a pending valuation from retaining mutable account entities. Standalone projections use the same helpers. New operations have no retained result to reuse after failures or changes.

Unit tests cover overview and refresh call counts, typed-service order and sequential execution, negative/zero/missing values, duplicate names/IDs/order, both standalone call orders, exact user/currency/timestamp isolation, failure retry, later account/value changes, and overlapping operations completed in reverse order. Concurrency coverage proves the new composition retains no cross-operation snapshot; it does not make concurrent EF calls on a shared DbContext safe. Each operation remains sequential.

Hosted integration coverage compares initial overview and warm refresh with standalone endpoints and verifies price, transaction-quantity and account-name changes without mutating the original overview. The write fixture deliberately retains the aggregate cache to exercise the narrower refresh. Unrelated overview data remains cached.

## Validation

- Regression before implementation: failed as expected because one refresh loaded investment accounts twice ([output](raw/red-test.txt), exit 2).
- `dotnet format code/FinanceManager.slnx`: exit 0; verify-no-changes with no restore: exit 0.
- `dotnet build code/FinanceManager.slnx`: exit 0, zero warnings/errors.
- Full unit suite: 2,286 passed, zero failures/skips.
- Full integration suite with `UseInMemoryDatabase=true`: 508 passed, zero failures, eight skipped. The skipped transaction-search cases require `FM_SEARCH_POSTGRESQL_CONNECTION` or `FM_SEARCH_SQLSERVER_CONNECTION`; those external providers were not configured.
- Architecture suite: 12 passed, zero failures/skips.
- Changelog check: 25 existing fragments valid. No new fragment: this preserves public behavior and changes internal allocation only.
- Full source/test/documentation diff reviewed; `git diff --check` passed. No UI changes, so no browser/screenshot check was run.

## Reproduction and retained evidence

From `code/FinanceManager.Benchmarks`, use the same seed directory for both revisions and do not reseed:

```sh
FM_BENCH_DATA_DIR=<same-seed-directory> FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release -- --validate
FM_BENCH_DATA_DIR=<same-seed-directory> FM_BENCH_RESEED=0 FM_BENCH_SEED_MONTHS=6 FM_BENCH_COLD_CACHE=0 dotnet run -c Release --no-build -- --filter '*Dashboard*' --artifacts <separate-run-directory>
```

The current guest seeder creates six months; the template is copied per process. Seed bytes are intentionally not committed. The two revisions used the unchanged default job: three warmups, ten measured iterations, 250 ms iteration target, in-process toolchain and MemoryDiagnoser. The bounded confirmation stops after this pair.

Retained [baseline JSON](raw/baseline-dashboard.json), [candidate JSON](raw/candidate-dashboard.json), [baseline output](raw/baseline-benchmark.txt), [candidate output](raw/candidate-benchmark.txt), endpoint validation, seed/source hashes, command exits and complete check transcripts are in [raw](raw). Retained text exports normalize trailing whitespace; original outputs remain under `/tmp/fm-1026-evidence`. The earlier restored experiment checkout and its artifacts were read only and remain untouched. No detector state was edited.
