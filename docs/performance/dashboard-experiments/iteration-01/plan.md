# Dashboard performance experiments — iteration 1

Baseline source: `f5178eddc8a18a101231b4418db02e618d82c740`. This is an experiment plan, not measured recommendations.

## Parent analysis

Dashboard.razor.cs loads an aggregate overview and passes models to seven cards; other cards fetch separately. DashboardQueryService sequentially composes that overview using a shared DbContext. CachingDashboardQueryService revalues asset distributions on cache hits: warm requests are not serialization-only. NetWorthService already batches investment valuation, and BondDashboardContext already shares referenced definitions and exact-date prices. The discovery report's proposed shared bond context is therefore already implemented. Imports and investment detail pages are outside this investigation.

Keep the layered monolith and reduce repeated work within existing services. Prefer request-local reuse and narrower reads. Wider caches require invalidation design; parallel queries require isolated contexts and would be preferable only if later profiling demonstrates independent I/O dominates. Neither is justified yet.

## Execution update

User steering: use Qwen3.8 instead of Ornith. Delegate exactly one experiment per task, receive and review its result before delegating the next. The interrupted Ornith task left uncommitted seed-window changes in GuestAccountSeeder.cs and BenchmarkEnvironment.cs, but no benchmark commit or result article. Preserve and assess those changes before establishing the experiment baseline; do not silently include them in an optimization comparison.

## Experiments (each independently against baseline)

1. **E01: Reuse bond price history across dates.** BondAccountEntry.GetPriceAt builds an entire history for each requested date. In the request-scoped bond context, compute the needed history once per unchanged entry/definition and reuse date lookups. Preserve gaps, posting dates, rate changes, rounding and capitalization exactly. Measure one-month/six-month overview and a long-lived bond fixture.
2. **E02: Remove metadata loading from net-worth account reads.** NetWorthService requests account graphs while using only balances and bond identifiers. Test `includeEntryMetadata: false` where supported and semantically equivalent. Measure cold overview, allocations and query count; verify values unchanged.
3. **E03: Reuse asset endpoint valuation between type/account distributions.** DashboardQueryService requests both consecutively, including on warm cache hits. Reuse request-local typed valuation inputs/results rather than pricing twice. Preserve type grouping, account identity, currency and price-refresh behavior; do not derive categories from display names.
4. **E04: Reuse liability inputs between type/account distributions.** LiabilitiesService fetches the same end-date currency accounts twice. Share request-local inputs while preserving category grouping, zero values, boundary entries and ordering. Measure cold overview and repository calls.
5. **E05: Precompute bond identifiers outside the daily net-worth loop.** NetWorthService calls GetStoredBondsIds for every account/day. Materialize identifiers once per loaded account. Measure allocations and overview latency, especially long windows; isolate this from E01.
6. **E06: Bound transaction-log fan-out.** Trace the dashboard transaction-log card through TransactionLogService, then replace per-account currency/bond reads with bounded cross-account queries if supported safely. Preserve global ordering/ties, authorization, descriptions and count. Benchmark its real endpoint plus scaled account counts; distinguish this independent card from overview latency.

## Measurement contract

Reuse FinanceManager.Benchmarks and its offline SQLite API host. First validate real non-empty responses; add repeatable benchmark coverage and correctness assertions, run unit tests, and commit the benchmark-only baseline before temporary optimization edits. Test-only local commits need no fabricated issue number; no external issue/PR creation or push is requested.

Use identical seed data, date windows, build configuration and machine for A/B comparisons. Record baseline commit, SDK/hardware, dataset identity/counts, commands, cache mode, repetitions, mean/median/p95 where available, variance/error and allocations. Measure both warm and cache-bypassed one-month/six-month overview; these are server timings, not browser paint. Add representative targeted/scaled fixtures where demo data cannot exercise the hypothesis. Never replace an API result with a synthetic microbenchmark claim.

For each experiment: capture baseline, implement only that experiment, test semantic parity and relevant regression tests, benchmark, retain raw outputs plus patch and an individual Markdown article, restore only experiment-owned production edits, verify baseline restoration, then proceed. Repeat paired runs for apparent gains near noise. No simultaneous benchmark processes. Do not change freshness semantics to manufacture a speedup. If infeasible, retain an article explaining the concrete blocker and attempted evidence, not invented results.

Articles must include hypothesis, exact change, before/after absolute and percentage timings, allocation differences, correctness evidence, limitations and reproduction instructions. Preserve all articles and patches through every later iteration. Finish with production source restored and benchmark tests retained. Review final diff and run required unit tests from code/.

## Review and stopping rule

After all individually reviewed Qwen3.8 experiments complete, a new Sol reviewer will inspect raw evidence, patches and correctness, rank credible improvements by absolute dashboard impact (separate endpoint/scenario rankings where incomparable), and recommend implementation or rejection. Parent will then select another round only for concrete untested bottlenecks or necessary confirmation/interaction tests. Stop when remaining candidates lack evidence of worthwhile benefit or are rejected on correctness/complexity. An environment blocker is an unresolved measurement, not proof that no optimization remains.

Final deliverable: all articles/raw evidence and one report with ranked recommendations, limitations, and proposed issues with scope, acceptance criteria, validation and estimated size. Permanent production implementation and publishing issues are not part of this experiment workflow.
