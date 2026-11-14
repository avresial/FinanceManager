# E01 fixture checkpoint

## Resumed checkpoint (incomplete)

Verified the original template has no `-wal` or `-shm` sidecar and SHA-256 `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e2a4da6bd1ec24b0e37867`. Immutable read-only schema inspection succeeded. Created `e01-data-63ac2cb175ab4e229d8fe34ed7b5bf63/seed-template.sqlite` as an isolated byte-identical copy, verifying its hash after copying. No fixture rows have been added. The plan now records the intended implementation. Repository selection, endpoint comparison, build and tests remain outstanding; this is not a validated fixture and must not be timed.

Blocked before fixture creation: the initial read-only SQLite URI schema inspection failed with `Error: unable to open database file`. See [plan](e01-fixture-plan.md) for observed selection path and proposed next steps. No fixture, endpoint assertion, build, test or timing was run. The original template was not modified; its SHA-256 was `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`, and the directory listing showed no WAL or SHM sidecars at inspection. No checkpoint was performed against the original.

The initial tracked worktree diff is preserved as [preexisting-worktree.patch](preexisting-worktree.patch), SHA-256 `7baadd33b0b0bce2037825ddf1c282e312bdb3a9a4066f8e20112c92502aabb6`. The existing untracked docs were not changed except for these new iteration-03 files. No candidate patch was applied.

## Fixture rows checkpoint

The original and isolated input initially both had SHA-256 `c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867`, with no sidecars. The original remains unchanged and serves as control. The isolated database now has SHA-256 `1fd1345066ad2a9e554082044e3fe9279d33d7404f0d84f6de7a20abf5c0d899`, no sidecars, and is at [seed-template.sqlite](e01-data-63ac2cb175ab4e229d8fe34ed7b5bf63/seed-template.sqlite).

[Builder](create-e01-fixture.py) was executed once; [captured output](e01-fixture-create.log) records exit 1 after the transaction committed, because a stale copy-side SHM remained after switching to DELETE mode. No process held the file according to `lsof`; the database independently reported DELETE mode and no WAL existed. The SHM was preserved as [e01-failed-run-shm.bin](e01-failed-run-shm.bin), then removed. The builder was corrected to consume pragma results and remove an orphan SHM only after closing the DELETE-mode copy and confirming no WAL. The corrected builder has not been rerun; the recorded execution did not produce its success JSON. No copy or reseed was performed.

[Independent immutable read-only verification](e01-fixture-verify.log) records account 5 (type 2, user 424242, currency 0), definition 9, entry 7, and method 9. Exactly one entry exists for that account or definition, posting 2016-05-02 00:00:00 UTC, value 1, unit value 100, with rate 0.05 and inclusive UntilDate through 2026-10-16. The first currency account's seed window is 2026-04-30 through 2026-10-16 UTC; the last-month start is 2026-09-16 UTC. Posting precedes the full-window start by 3650 days. Counts are 5 accounts, 9 definitions, 7 bond entries and 9 methods; foreign key check is empty. Account type and operator values and pricing behavior were checked against the domain enums and `BondAccountEntry.GetPrice` / `BondCalculationMethod.IsActiveAt`.

**Endpoint contribution and actual repository selection are NOT VALIDATED.** No build, tests, endpoint calls or timing were run in this checkpoint.

## Functional control checkpoint

See [validation evidence](e01-fixture-validation.md). Both isolated functional calls succeeded and the start/end price proofs and account distribution match within 0.01. Intermediate daily price comparisons were not performed; coverage is limited to those proven points. Parent review remains required before A/B.
