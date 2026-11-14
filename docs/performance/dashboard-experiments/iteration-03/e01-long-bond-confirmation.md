# E01 — long-held-bond API confirmation

**Decision:** File [#1028](https://github.com/avresial/FinanceManager/issues/1028). The E01 candidate substantially improved the tested cold API workload in both pairs, with complete baseline/candidate response parity. This supersedes the previous E01 recommendation to defer pending a representative workload. The fixture is deliberately synthetic; the prevalence and production impact remain unknown.

One additional bond account holds a single entry from 2016-05-02, 3,650 days before the six-month window. The actual repository selected that entry on all 170 days, and control-vs-fixture endpoint deltas at the full-range start and shared end matched independently priced values within 0.01. Both asset distributions included the synthetic account. Intermediate fixture-control buckets were not independently priced. Candidate-vs-baseline deep JSON equality passed for *all* returned data in both windows. Candidate tests: 2,259 passed; after restoration: 2,253 passed.

## Repeated cold API results

A = baseline; B = E01 candidate. Four sequential BenchmarkDotNet runs on one fixed template; means are ms and allocations bytes/op. Error is each run's 99.9% CI half-width, not a formal between-run test.

| Run | Six-month mean ± error | Allocated | One-month mean ± error | Allocated |
|---|---:|---:|---:|---:|
| A1 | 186.27 ± 9.06 | 94,982,416 | 57.27 ± 1.70 | 20,410,042 |
| B1 | 65.87 ± 4.93 | 7,307,242 | 38.57 ± 0.90 | 4,702,541 |
| A2 | 183.53 ± 10.56 | 94,891,008 | 56.17 ± 0.79 | 20,414,056 |
| B2 | 71.08 ± 3.91 | 7,277,214 | 38.25 ± 0.85 | 4,700,948 |

Six-month pair changes: mean -64.6% and -61.3%; allocations -92.3% and -92.3%.

One-month pair changes: mean -32.7% and -31.9%; allocations -77.0% and -77.0%.

## Method and limitations

Cold `GET api/Dashboard/overview` through the real in-process API host and local SQLite, with `FM_BENCH_COLD_CACHE=1`, `FM_BENCH_RESEED=0`, fixed isolated seed, BenchmarkDotNet 0.15.8, .NET 10.0.10, Apple M1 Max. Warm-cache hits do not calculate the net-worth series, so no warm speedup is asserted. Not browser paint, network or production DB performance. Same isolated template SHA256 `1fd1345066ad2a9e554082044e3fe9279d33d7404f0d84f6de7a20abf5c0d899` across runs; original template preserved. The fixture builder initially exited 1 after committing due to an orphan SHM; isolated DB was independently checked and frozen; the corrected builder was not rerun. Existing pre-task source edits remain. Candidate-only code/tests were restored after B2; retained benchmark validator changes remain in the worktree.

- [Fixture and control validation](e01-fixture-validation.md)
- [A1 baseline](e01-a1-article.md) · [B1 candidate](e01-b1-article.md) · [A2 baseline](e01-a2-article.md) · [B2 candidate](e01-b2-article.md)
- [Candidate patch](../iteration-01/e01-candidate.patch) · [Final restoration test log](raw/e01-restoration-final-tests.log) · [Test exit](raw/e01-restoration-final-tests.exit)
- [Fixture preparation and known builder limitation](e01-fixture.md)
