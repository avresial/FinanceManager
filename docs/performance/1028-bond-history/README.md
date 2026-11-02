# Request-local dashboard bond history — #1028

Measured 2026-11-02. Baseline: `b06ade0b2dac88e24634350fdf4a1134a1a05bf2`; candidate: the two production files identified by [source hashes](raw/candidate-source.sha256). .NET SDK 10.0.302/runtime 10.0.10, BenchmarkDotNet 0.15.8, Apple M1 Max, macOS 26.6.2.

## Result and limits

One bounded sequential cold baseline/candidate pair completed both dashboard windows. The frozen SQLite seed was byte-identical before and after both runs: `998db819447b30982633d76618bb7c901033e1f203aec850daededb5f641577d`. No benchmark overlapped builds or tests in this checkout. Both jobs used three warmups, ten measured iterations, a 250 ms iteration target, in-process toolchain and MemoryDiagnoser.

| Window | Baseline mean ms | Candidate mean ms | Baseline B/op | Candidate B/op |
| --- | ---: | ---: | ---: | ---: |
| Six months (170 days) | 182.410 | 66.602 | 94,673,816 | 7,116,686 |
| One month (31 days) | 56.798 | 38.068 | 20,331,896 | 4,625,743 |

These descriptive measurements cover routing, auth, application/domain services, real repositories, SQLite and full JSON serialization over an in-memory API transport. Cache is bypassed with `FM_BENCH_COLD_CACHE=1`. They do not measure sockets, TLS, browser paint, production database latency, concurrency or typical portfolios. The one additional bond is a synthetic stress case; its production prevalence is unknown. This single pair is not a new replicated latency claim. BenchmarkDotNet could not raise process priority on macOS in either run. Its within-run confidence intervals are retained in the full outputs; its Ratio column compares windows within each run, not revisions.

[Full baseline JSON](raw/baseline/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [full candidate JSON](raw/candidate/results/FinanceManager.Benchmarks.Benchmarks.DashboardBenchmarks-report-full.json), [baseline output](raw/baseline-benchmark.log), [candidate output](raw/candidate-benchmark.log), and command exits are retained under [raw](raw). This confirmation stopped after the requested pair. The prior E01/E03 experiments and their checkout were read only and were not restarted.

## Implementation and parity

`NetWorthService.GetNetWorth(start, end)` keeps a reference-keyed history dictionary only within that invocation. All account and definition loading finishes before its synchronous descending daily loop. The first selected date for an entry is its latest required date: domain `GetPrice` builds through that date, at most the request end. This also avoids evaluating malformed future methods for superseded entries. The dictionary is discarded when the operation ends; loaded snapshots cannot change within the loop. A new operation builds from its own inputs. No history key must persist across mutable entry or definition versions.

The existing #747 request-scoped exact-date cache takes precedence through a lazy calculation callback. Point callers retain their original pricing path. Domain calculations, date selection, duplicate precedence, rounding, 365-day capitalization, definition loading, API contracts, cache refresh policy and sequential shared-DbContext access are unchanged. No new service, DI registration, database schema, dependency or cross-request cache is introduced.

Unit tests cover ascending/descending/shuffled history lookups, exact-price sharing in both call orders, before/posting/horizon dates, rates and expiry/gaps, multiple/duplicate/changing entries and accounts, boundary entries, intraday selection, zero holdings, errors, later operations and fresh requests. The allocation regression first failed against baseline (15,895,912 bytes for 31 days versus 512,624 for one history; [red test](raw/red-test.log), exit 2), then passed with the candidate. Its broad relative budget detects per-day rebuilding without asserting machine-specific latency.

Hosted API tests use real repositories and the complete dashboard composition, with a daily point-pricing net-worth oracle. Both one/six-month responses match in full, and each bucket proves the selected long-held entry and exact rounded price. They also verify warm overview hits skip net worth, an HTTP bond-entry PUT refreshes the next overview without manual invalidation, and separate direct definition/unit/rate edits produce fresh responses after explicit user invalidation. They use isolated EF in-memory databases; the separate cold comparison uses SQLite.

The frozen multi-account SQLite fixture additionally proves [complete baseline/candidate JSON parity](raw/parity.json) for both windows, including all arrays and ordering. Real repository selection finds entry 7 (account 5, bond 9), held 3,650 days before the full window, on every one of 170 dates. A separate control response without this account proves its independently point-priced contribution in all 170/31 buckets, allowing 0.01 for subtracting independently rounded portfolio totals. The synthetic account's asset row also matches its exact endpoint price. See [baseline response](raw/baseline-response.json), [candidate response and daily selection proof](raw/candidate-response.json), [control response](raw/control-response.json), and the runnable [comparison](compare-responses.py).

## Fixture repair and reproduction

The retained experimental builder failed after commit because switching a copied WAL database to DELETE mode left an orphan SHM on this SQLite build. A narrow isolated probe reproduced that behavior. The replacement [builder](create-fixture.py) uses the existing Python/SQLite standard library workflow: `VACUUM INTO` from an immutable read-only source creates a standalone DELETE-mode staging database before any fixture writes. It validates the source hash/no-sidecar preconditions, transaction, foreign keys, integrity, closed staging file, and unchanged source; an exclusive hard link publishes the completed file atomically within the same filesystem. Existing targets are rejected. No source or orphan sidecar is deleted.

Two final independent builder executions produced byte-identical fixtures, matching the two earlier successful copies; all four hashes are retained in [reproduction evidence](raw/fixture-reproduction.json). Existing-target rejection preserved the target hash. The original experiment seed remained `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`, with no sidecars. The builder expects this frozen six-month guest seed shape (cash account 1 and bond account 4); it is a bounded evidence tool, not a general database migration. Seed databases are intentionally not committed.

From the repository root, give the builder a closed seed, a **new** target and its verified SHA-256:

```sh
python3 docs/performance/1028-bond-history/create-fixture.py <frozen-source.sqlite> <data-dir>/seed-template.sqlite <source-sha256>
```

Use the same generated template for both revisions and never reseed. For a fresh reproduction, the baseline is `b06ade0b` with only this PR's benchmark validator/Program/BenchmarkEnvironment changes applied; its two production services must retain the [baseline hashes](raw/baseline-source.sha256). Build each revision in Release before measuring, outside the benchmark process. From `code/FinanceManager.Benchmarks`, run sequentially for each revision, replacing paths with absolute, separate output paths:

```sh
FM_BENCH_DATA_DIR=<data-dir> FM_BENCH_RESEED=0 FM_BENCH_COLD_CACHE=1 LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet run -c Release --no-build -- --validate-long-bond --bond-mode fixture --bond-output <revision-response.json>
FM_BENCH_DATA_DIR=<data-dir> FM_BENCH_RESEED=0 FM_BENCH_COLD_CACHE=1 LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet run -c Release --no-build -- --filter '*Dashboard*' --artifacts <revision-artifacts>
```

For the control, copy the original closed seed to another data directory and use `--bond-mode control`; it must have no synthetic account. Record template hashes before and after every run. The comparison script expects `baseline-response.json`, `candidate-response.json`, `control-response.json` and the six `*-before/after.sha256` files in its argument directory:

```sh
python3 docs/performance/1028-bond-history/compare-responses.py <evidence-directory>
```

## Repository validation

- `dotnet format code/FinanceManager.slnx`, then verify-no-changes: exit 0.
- Strict solution build and Release benchmark build: exit 0, zero warnings/errors.
- Full unit suite: **2,300 passed**, no failures/skips.
- Full integration: **512 passed**, no failures; eight external transaction-search provider cases skipped because `FM_SEARCH_POSTGRESQL_CONNECTION`/`FM_SEARCH_SQLSERVER_CONNECTION` are unset.
- Architecture: **12 passed**, no failures/skips.
- Changelog check: 26 existing fragments valid. No new fragment for this behavior-preserving internal reuse.
- Full diff reviewed; `git diff --check` passed. No UI files changed, so no app/browser/screenshot check was required.

Full transcripts and exits are in [raw](raw). Text exports trim trailing whitespace and replace absolute local checkout/evidence paths with placeholders; original outputs remain in `/tmp/fm-1028-evidence`. [SHA256SUMS](SHA256SUMS.txt) describes the retained exports.

## Bounded review remediation

The two review fixes change validation only; production pricing and all benchmark/response/seed artifacts above are unchanged. No benchmark was rerun.

- `compare-responses.py` now matches the full proof's exact dates/count to the fixed window, checks full/month response bounds, and requires each baseline/candidate/control daily series to contain the expected dates exactly once. [Retained-data revalidation](raw/review-fixes/parity.json) passed. Run `python3 docs/performance/1028-bond-history/check-date-coverage.py` for the regression check: [all 16 corruptions](raw/review-fixes/date-coverage.log) were rejected, covering missing dates and duplicate/missing pairs in each response independently and in all three together, for both windows. Corruptions use temporary copies and preserve retained evidence.
- The hosted test now calls `PUT api/BondEntry` and reads the overview immediately without manual invalidation, then compares the complete result with point pricing. The separate tracked definition edit still saves and invalidates independently before its own point-pricing comparison. Both one/six-month cases passed ([focused API transcript](raw/review-fixes/focused-api.log)).
- Remediation validation: formatting passed; strict solution build passed with zero warnings/errors; all 2,300 required unit tests passed. [Transcripts and exits](raw/review-fixes) are retained. The broader integration and architecture totals above belong to the original implementation checks; only the affected hosted test class was rerun for this validation-only fix. Final diff and artifact checksums were reviewed.
