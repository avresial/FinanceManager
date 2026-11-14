# Iteration 2 — Confirm E03 warm investment valuation reuse

Parent decision after Sol review: only E03 merits another measurement round now. Its six-month warm timing and allocation observations justify confirmation; cold and one-month latency remain unproven. E01 requires workload evidence before a scaled API fixture; E02/E04 are rejected, E05 is cleanup only, E06 already exists.

Use the exact retained iteration-01/e03-candidate.patch, unchanged seed SHA256 c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867, HEAD 8f987e2e5f909716ece1d396f96c2701fbc3707a and preserved harness edits. Sequential warm A1/B1/A2/B2, one measurement checkpoint per delegation; no simultaneous benchmarks. A is restored baseline, B is the unchanged candidate. Each run measures both dashboard windows and records full JSON, process exit, seed/source integrity and a separate Markdown article. Restore B2 afterward and run unit tests. Retain every artifact.

Parent reviews same-scope freshness and call paths before recommending adoption. Repeated timing direction and absolute size matter; within-run CIs are descriptive and do not replace run-level replication. Allocation evidence is reported separately. No browser or production-database claims. Stop this round after four valid runs; do not chase noise indefinitely. Sol reviews the combined evidence before the final report.
