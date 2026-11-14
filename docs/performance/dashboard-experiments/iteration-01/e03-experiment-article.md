# E03 — Share investment endpoint valuation within a request

Status: measured and restored. Recommendation: retain as a conditional candidate for Sol review; do not claim a general dashboard latency win.

## Hypothesis and change
Type and account asset distributions repeat investment endpoint valuation, including during warm-cache asset refresh. The temporary patch added an instance-local dictionary in AssetsServiceInvestment keyed by user, currency scalar fields and exact as-of time. It snapshots successful batched values and account inputs, preserving negative values in the type total and positive-only account rows. No cross-request cache, parallel DbContext access or public contract change.

## Measurements
Times below are milliseconds; allocations are bytes per operation. Error is BenchmarkDotNet's within-run 99.9% interval half-width, not a between-run significance test. All runs use the same seed and retained harness modifications.

| Run | Window | Mean | Error | Median | P95 | Allocated |
|---|---|---:|---:|---:|---:|---:|
| baseline-warm | 6 mo | 15.685 | 0.407 | 15.739 | 16.015 | 661,333 |
| baseline-warm | 1 mo | 15.497 | 1.380 | 15.550 | 16.788 | 628,380 |
| replacement-warm | 6 mo | 14.748 | 0.293 | 14.728 | 15.049 | 614,117 |
| replacement-warm | 1 mo | 13.864 | 1.183 | 13.813 | 14.781 | 584,520 |
| confirm-baseline-warm | 6 mo | 15.538 | 0.455 | 15.396 | 15.964 | 661,402 |
| confirm-baseline-warm | 1 mo | 13.955 | 1.273 | 13.540 | 15.019 | 630,489 |
| baseline-cold | 6 mo | 89.102 | 11.236 | 93.531 | 94.574 | 5,613,744 |
| baseline-cold | 1 mo | 56.346 | 3.164 | 55.759 | 59.574 | 3,028,910 |
| replacement-cold | 6 mo | 84.686 | 7.220 | 85.659 | 90.060 | 5,519,678 |
| replacement-cold | 1 mo | 56.295 | 3.035 | 55.988 | 59.417 | 2,982,468 |
| confirm-baseline-cold | 6 mo | 83.882 | 6.404 | 83.633 | 89.207 | 5,633,284 |
| confirm-baseline-cold | 1 mo | 57.177 | 3.260 | 57.385 | 60.074 | 3,033,976 |

## Interpretation

- warm, 6 months: candidate versus original baseline -5.98% time, -7.14% allocations; versus restored baseline -5.08% time, -7.15% allocations.
- warm, 1 months: candidate versus original baseline -10.54% time, -6.98% allocations; versus restored baseline -0.65% time, -7.29% allocations.
- cold, 6 months: candidate versus original baseline -4.96% time, -1.68% allocations; versus restored baseline +0.96% time, -2.02% allocations.
- cold, 1 months: candidate versus original baseline -0.09% time, -1.53% allocations; versus restored baseline -1.54% time, -1.70% allocations.

Warm six-month timing and warm allocation reductions warrant further consideration. One-month timing benefit is not stable against the restored baseline. Cold latency does not show a repeatable benefit across baseline orderings. One candidate run per mode and baseline drift prevent broad statistical claims. Lower allocations are consistently observed, but scope and invalidation cost still matter.

## Correctness and restoration
Candidate tests cover both call orders, negative totals, identity despite duplicate names, fresh instances, user/currency/exact-time isolation and failed valuation retry. Candidate suite: 2,258 passed. Restored suite: 2,253 passed. Two candidate files were restored exactly to HEAD 8f987e2e5f909716ece1d396f96c2701fbc3707a; candidate tests remain in the patch. GuestAccountSeeder and BenchmarkEnvironment modifications were preserved. Same-scope writes are not invalidated; any permanent implementation must establish that all consumers use read-only scopes or add explicit invalidation.

## Reproduction and evidence
Use the exact commands in the linked checkpoint reports. Release, English locale, six-month seed, no reseed, Dashboard filter; FM_BENCH_COLD_CACHE=0/1 chooses warm/bypassed modes. Seed SHA256: c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867. Machine: Apple M1 Max, .NET SDK10.0.302/runtime10.0.10, BDN0.15.8. These are offline SQLite in-process API measurements, not browser paint, network latency, PostgreSQL or SQL Server results.

Earlier candidate artifacts collided in a shared directory. They are retained but excluded from these tables; only replacement runs are used for candidate measurements. This is a baseline-candidate-restored-baseline sequence, not two independent candidate replications.

- [Baseline](e03-baseline.md)
- [Implementation](e03-implementation.md)
- [Candidate patch](e03-candidate.patch)
- [Warm candidate and raw links](e03-replacement-warm.md)
- [Cold candidate and raw links](e03-replacement-cold.md)
- [Restoration and warm confirmation](e03-restoration-warm.md)
- [Cold baseline confirmation](e03-confirm-baseline-cold.md)
- [Pre-restoration evidence](evidence/e03-pre-restoration-worktree.patch)
