# FinanceManager optimization audit

Date: 2026-09-05\
Baseline: local `develop`, commit `8185628e7e020ccb8f6972339ff78064758c31ea`\
Scope: source-level audit of database access, valuation, dashboard composition, imports, browser rendering/caching, and selected background processes.

## Parent idea

Use this issue as an exploratory parent for a FinanceManager performance roadmap. It is intentionally not an implementation ticket. After measurement and prioritization, split the findings into smaller, independently testable issues.

Related existing work should be coordinated rather than duplicated: #728 covers bounded NBP range lookups; completed work includes dashboard read-model batching (#395/#544), linear currency balance aggregation (#704), account/entry batching (#411), and earlier FX conversion work (#447).

The strongest opportunities are to calculate bond histories once, batch import writes, and reduce dashboard/account-detail overfetching. There is also a concrete mismatch between the FX range-cache write keys and point-cache read keys.

This is a static audit, not a production performance assessment. Code patterns below were traced through their callers and implementations. Impact estimates describe work that could be eliminated; no latency, throughput, allocation, or database-plan measurements were collected. The initial working tree was clean. Only this report was added; no application changes, commits, remote operations, or live financial-data operations were performed.

## Prioritized opportunities

P1 means a strong candidate for the first implementation wave; P2 means worthwhile for larger histories or portfolios. Confidence refers to the code mechanism, not a measured user-visible speedup. Effort is an indicative implementation range, subject to regression scope.

| ID | Opportunity | Priority | Expected benefit | Confidence | Effort |
|---|---|---|---|---|---|
| O1 | Compute bond accrual history once and advance entry cursors | P1 | Remove quadratic accrual replay and repeated entry scans | High | M |
| O2 | Batch import persistence and group input dates once | P1 | Fewer commits/cache invalidations; less import CPU | High | M–L |
| O3 | Query recent currency/bond transactions across accounts | P1 | Replace per-account reads with bounded result sets | High | M |
| O4 | Repair FX range-to-point cache reuse | P1 | Eliminate unused cache entries and redundant point reads | High | S–M |
| O5 | Scope account-detail appreciation to the requested account | P1 | Avoid valuing the user's entire portfolio for one page | High | M |
| O6 | Project/aggregate investment history at the database boundary | P2 | Fewer transferred rows and entity allocations | High | M |
| O7 | Skip price histories for positions closed before the window | P2 | Avoid irrelevant quote/FX/provider work | High | M |
| O8 | Reuse bond definitions and derived dashboard values | P2 | Fewer catalog queries and repeated calculations on cold loads | High | M |
| O9 | Bound investment history payloads and rendered rows | P2 | Lower WASM memory, rendering, and enrichment cost | High | L |
| O10 | Batch database work in missing-FX-date resolution | P2 | Reduce serialized point reads and per-rate saves | High | M–L |

Benefits overlap: O1 and O8 both affect dashboard bond work; O4 and O10 both affect FX. Do not sum their expected savings independently.

## O1 — Bond charts rebuild historical accrual for each output day

**Evidence:** [BondAccountEntry.cs:25](../../code/FinanceManager.Domain/FinancialAccounts/Bond/Entities/BondAccountEntry.cs#L25); [BondAccount.cs:148](../../code/FinanceManager.Domain/FinancialAccounts/Bond/Entities/BondAccount.cs#L148); [BondEntryExtension.cs:15](../../code/FinanceManager.Domain/FinancialAccounts/Bond/Extensions/BondEntryExtension.cs#L15); [NetWorthService.cs:50](../../code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs#L50).

`BondAccountEntry.GetPriceAt(date, details)` calls `GetPrice`, which allocates a daily dictionary and loops from the entry's posting date to the requested date, then returns just one value. `BondAccount.GetDailyPrice` calls this inside its day/instrument loop. `NetWorthService.GetNetWorth(start, end)` also invokes the point calculation inside a daily loop.

For one unchanged entry spanning D days, requesting every daily value can replay roughly `D × (D - 1) / 2` accrual iterations. A synthetic 3,650-day history implies 6,659,425 iterations, before entry selection and other accounts; this is a loop-count illustration, not a benchmark. Entry selection additionally filters the account's entry list for each day and instrument, including scans on dates without transactions.

**Recommendation:** partition entries by instrument and sort once; advance a cursor as dates advance. Compute each effective entry's accrual history once for its required interval, or use an incremental accrual state that emits daily values. Reuse the resulting series in chart/net-worth composition. Keep existing domain calculations in Domain and orchestration in Application.

**Expected result:** accrual work approaches linear growth in the dates actually valued, rather than replaying the full prefix for every date. Entry selection loses its repeated whole-list scan.

**Validate:** compare every output against the existing algorithm across multi-year fixtures, multiple intraday entries, opening-boundary entries, zero holdings, missing calculation methods, rate changes, and capitalization boundaries. Preserve the current rounding and 365-day capitalization semantics; a closed-form formula must not silently change them. Measure CPU and allocated bytes on 1/5/10-year ranges.

## O2 — Imports persist one row at a time and repeatedly scan date ranges

**Evidence:** [CurrencyAccountImportService.cs:55](../../code/FinanceManager.Application/FinancialAccounts/Currencies/Import/CurrencyAccountImportService.cs#L55); [BondAccountImportService.cs:58](../../code/FinanceManager.Application/FinancialAccounts/Bond/Import/BondAccountImportService.cs#L58); [CurrencyEntryRepository.cs:16](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Currencies/Repositories/CurrencyEntryRepository.cs#L16); [CachedAccountEntryRepository.cs:240](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Shared/Repositories/CachedAccountEntryRepository.cs#L240).

Currency and bond imports walk every calendar day between the earliest and latest input dates. Each iteration filters the complete import and existing-entry collections. Accepted rows then use the single-entry `Add(..., recalculate: false)` path. Currency persistence calls `SaveChangesAsync` per row; its cache decorator also invalidates account/user caches after each successful add. Deferring balance recalculation until the end already helps, but does not batch these saves.

**Recommendation:** pre-group input and existing entries by date, process only input dates in the current descending order, and persist validated conflict-free rows in bounded batches. Existing collection-add repository overloads are a starting point. Invalidate after each committed batch and recalculate once from the earliest affected persisted entry. Apply the same principle to resolved-conflict processing, which currently can recalculate after individual mutations.

**Expected result:** for N accepted rows and batch size B, explicit save calls can approach `ceil(N/B)` rather than N, excluding final recalculation and exceptional retries. This is not a claim of one SQL statement per batch. Date classification moves away from repeated `O(D × (N + E))` scans.

**Validate:** preserve duplicate multiplicity, partial failures, conflict callbacks, progress, cancellation and committed-data repair. Verify generated IDs are available to the recalculation path; the single currency add currently creates a separate entity, so blindly carrying the input object's ID into a batch refactor is unsafe. Compare 1k/10k-row imports on both relational providers, counting saves, SQL commands, invalidations, and final balances. Split batching and conflict-resolution changes if needed to keep delivery bounded.

## O3 — The dashboard's recent log still fans out across currency/bond accounts

**Evidence:** [TransactionLogService.cs:34](../../code/FinanceManager.Application/Dashboard/TransactionLogService.cs#L34); [InvestmentTransactionRepository.cs:24](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Investments/Repositories/InvestmentTransactionRepository.cs#L24).

`TransactionLogService.GetLastTransactions` loads up to `count` currency entries per account and repeats this for bond accounts, then sorts and takes `count` globally. Bond descriptions add one details lookup per distinct uncached bond ID. Investment transactions already use `GetMostRecentByAccounts` and a database-side `Take(count)`.

**Recommendation:** add equivalent cross-account recent-entry queries for currency and bonds, projecting the log fields and bond name directly or resolving names in one batch. Fetch at most `count` candidates per account type and merge those small lists.

**Expected result:** cold reads change from account-count-dependent entry queries and up to `(currency accounts + bond accounts) × count` candidate rows to a fixed number of entry queries and at most `3 × count` candidates across the three types. Warm repository caches already reduce database reads; measure cold and warm cases separately.

**Validate:** 1/10/100-account fixtures, empty types, cross-type date ties, current `EntryId` ordering, deleted/missing bond details, and ownership restrictions. Reuse `TransactionLogServiceTests` and add relational query-count coverage.

## O4 — FX range writes do not populate the cache used by point reads

**Evidence:** [CachedCurrencyExchangeService.cs:30](../../code/FinanceManager.Application/FinancialAccounts/Currencies/ExchangeRates/CachedCurrencyExchangeService.cs#L30); [CurrencyExchangeService.cs:17](../../code/FinanceManager.Application/FinancialAccounts/Currencies/ExchangeRates/CurrencyExchangeService.cs#L17).

`CachedCurrencyExchangeService.GetExchangeRateResultAsync` reads `EXCHANGE_RATE_RESULT_...` with a `CurrencyExchangeRateResult` value. Its range overload writes `EXCHANGE_RATE_...` with a decimal. A source search found no production reader of that latter key. The range overload also always invokes the inner range service.

**Recommendation:** first remove the ineffective writes or replace them with a shared, typed key contract. Populate point-result cache entries only for authoritative resolved rates. Range output can contain carried-forward values after the resolution cap; caching every non-null range value as a successful point lookup would change financial semantics. Add provenance to the internal range result if necessary before reusing it this way.

**Expected result:** remove dead cache allocations; where actual resolved rates are safely shared, a subsequent point request avoids the inner database/provider chain. Do not assume current repeated point lookups are uncached: success, failure, and publication-pending point results already have caching.

**Validate:** range then point should reuse genuine results; carried values must not masquerade as published rates. Cover expiry, inverse rates, same currency, currency normalization, and current-day publication-pending behavior. Existing `CachedCurrencyExchangeServiceTests` cover repeated point reads but do not cover this range-to-point interaction.

## O5 — Opening one investment account requests portfolio-wide appreciation

**Evidence:** [InvestmentAccountDetailsPageContent.razor.cs:387](../../code/FinanceManager.Components/Features/FinancialAccounts/Components/InvestmentAccountComponents/InvestmentAccountDetailsPageContent.razor.cs#L387); [AssetsServiceInvestment.cs:98](../../code/FinanceManager.Application/FinancialAccounts/Investments/Assets/AssetsServiceInvestment.cs#L98).

The account chart calls `GetUnrealizedGainLossPerAccount(userId, currency, dateEnd)` and then selects the current account from the response. The investment implementation enumerates all investment accounts, queries transactions separately per account, and computes their instrument results.

**Recommendation:** introduce an ownership-checked account filter through the existing service/API boundary. Use it for account details. Separately batch transaction loading for the intentionally portfolio-wide view, sharing repeated price/rate inputs where appropriate.

**Expected result:** account-page appreciation cost becomes proportional to the selected account. A user with many unrelated accounts should not pay their valuation cost during this page load.

**Validate:** compare the filtered result with the corresponding portfolio result; verify cross-user account rejection, excluded instruments, missing rates, and aggregate rounding. Count queries and price-provider calls with 1 versus 50 accounts.

## O6 — Investment calculations load more history and entity data than they need

**Evidence:** [InvestmentTransactionRepository.cs:24](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Investments/Repositories/InvestmentTransactionRepository.cs#L24); [InvestmentTransactionRepository.cs:93](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Investments/Repositories/InvestmentTransactionRepository.cs#L93); [InvestmentValuationService.cs:132](../../code/FinanceManager.Application/FinancialAccounts/Investments/Valuation/InvestmentValuationService.cs#L132).

`GetByAccounts` loads all transactions for the account IDs, including listing entities and ordering the entire result. Valuation/capital/benchmark callers then filter by end date in memory. `GetHoldingsAsOf` filters and projects in SQL, but transfers every matching transaction and groups it in memory.

**Recommendation:** add purpose-specific reads: database aggregation by account/listing for point holdings, an end-date-bounded narrow projection for historical calculations, and opening holdings plus in-window deltas where the calculation permits it. Keep historical purchase cash flows available for capital and cost-basis calculations; they cannot simply be dropped at the chart's start date.

**Expected result:** holdings reads return position counts rather than transaction counts; historical calculations avoid future rows and unnecessary entity graphs. This remains within existing repository contracts and layers. Projection and bounded results follow [EF Core efficient-query guidance](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).

**Validate:** compare buys, sells, fees, fractional quantities, minor quote-unit multipliers, and transactions on date boundaries. Inspect generated SQL and actual execution plans on SQL Server and PostgreSQL. InMemory tests cannot establish SQL translation or query performance.

## O7 — Historical valuation prices instruments that were already closed

**Evidence:** [InvestmentValuationService.cs:132](../../code/FinanceManager.Application/FinancialAccounts/Investments/Valuation/InvestmentValuationService.cs#L132); [InvestmentPriceProvider.cs:85](../../code/FinanceManager.Application/Assets/Pricing/InvestmentPriceProvider.cs#L85).

`GetAccountValueSeriesAsync` prices every distinct listing with any transaction on or before the end date. It does not first exclude listings with zero opening holdings and no in-window position changes. `BuildAccountSeries` subsequently skips zero holdings, after quote history, seed, and FX work may already have occurred. The point-valuation batch already drops zero positions; the series path does not apply an equivalent relevance gate.

**Recommendation:** compute opening holdings and in-window activity before requesting prices. Exclude listings that cannot contribute any nonzero position during the requested window. Retain instruments bought and sold within the window, even when closing holdings are zero. Apply an independently justified gate to benchmarks as well.

**Expected result:** shorter chart loads for long-lived accounts with many retired instruments, especially when their historical quotes are missing.

**Validate:** a listing sold before the range triggers zero price-series calls; a listing opened and closed inside the range remains represented; cross-account offsets must not hide real per-account positions.

## O8 — Dashboard composition rereads bond definitions and recalculates shared values

**Evidence:** [DashboardQueryService.cs:27](../../code/FinanceManager.Application/Dashboard/DashboardQueryService.cs#L27); [BondDetailsRepository.cs:29](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Bond/Repositories/BondDetailsRepository.cs#L29); [AssetsServiceBond.cs:68](../../code/FinanceManager.Application/FinancialAccounts/Bond/Assets/AssetsServiceBond.cs#L68); [NetWorthService.cs:50](../../code/FinanceManager.Application/MoneyFlow/NetWorth/NetWorthService.cs#L50).

Net-worth, bond balance, and bond asset paths each load the full bond catalog with calculation methods. Bond end-assets per account and per type independently calculate substantially the same endpoint values. EF tracking does not make a repeated LINQ query disappear. The dashboard has a five-minute outer cache and `AccountRepository` has request-local account caching, so this finding concerns the remaining catalog/calculation duplication on cache misses.

**Recommendation:** use a request-scoped immutable bond-definition lookup, preferably restricted to referenced IDs, and derive multiple aggregates from one calculated endpoint result. Reuse O1's computed history when exact valuation inputs match. Start with request-local reuse to avoid a new cross-request invalidation problem.

**Expected result:** fewer catalog reads and duplicated bond calculations per cold dashboard response. Savings depend on bond catalog size, account count, and cache hit rate.

**Validate:** instrument query counts across one overview request and compare every card result. Test missing definitions and edits; do not turn tracked mutation entities into a shared mutable cache.

## O9 — Investment history downloads and renders the full account list

**Evidence:** [InvestmentAccountDetailsPageContent.razor:42](../../code/FinanceManager.Components/Features/FinancialAccounts/Components/InvestmentAccountComponents/InvestmentAccountDetailsPageContent.razor#L42); [InvestmentAccountDetailsPageContent.razor.cs:500](../../code/FinanceManager.Components/Features/FinancialAccounts/Components/InvestmentAccountComponents/InvestmentAccountDetailsPageContent.razor.cs#L500); [InvestmentTransactionRepository.cs:24](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Investments/Repositories/InvestmentTransactionRepository.cs#L24).

The account page fetches all transactions. The Razor render filters, sorts, groups, and emits every matching day card. Server transaction enrichment also reads the whole account. A date filter reduces visible records after downloading them; it does not bound the payload. Broad/all-history ranges can render many components, while refreshes repeat list transformations.

**Recommendation:** use a bounded server history query with stable `(TradeDate, Id)` continuation and server-side search/filtering. Request row valuations only for the visible page. Keep account-wide holdings/chart totals independently computed so pagination does not change them. As a smaller first step, cache filtered day groups until inputs change and virtualize day groups after checking variable-height behavior.

**Expected result:** lower payload, WASM heap use, enrichment work, and rendered component count for large accounts. Virtualization alone will not reduce server work or download size.

**Validate:** mobile and desktop at 100/1k/10k trades; measure response bytes, live component count, filter latency, and allocations. Exercise page boundaries inside a day, edits/deletes, global search, empty pages, and snapshot restoration. Preserve the [snapshot contract](../codebase/UI-SNAPSHOTS.md): restored UI must still refresh from the server.

## O10 — Missing FX dates repeat serialized database work

**Evidence:** [CurrencyExchangeService.cs:17](../../code/FinanceManager.Application/FinancialAccounts/Currencies/ExchangeRates/CurrencyExchangeService.cs#L17); [ExchangeRateRepository.cs:13](../../code/FinanceManager.Infrastructure/Features/FinancialAccounts/Currencies/Repositories/ExchangeRateRepository.cs#L13).

The range resolver first reads direct and inverse stored rates. For missing dates it launches batches of up to 50 point resolutions, capped at 60 dates per call. Each point resolution checks direct/inverse storage again and persists individual successes. `ExchangeRateRepository` explicitly serializes context access with a semaphore, so the fan-out helps provider HTTP work but does not make those database operations parallel.

**Recommendation:** separate batched storage discovery from provider resolution. Reuse the known missing-date set, load any required USD-leg ranges together, perform bounded provider work, and persist successful rates in a batch. Keep concurrency configurable to provider behavior; increasing the batch size is not the primary optimization.

**Expected result:** fewer serialized checks and saves when historical rate coverage is incomplete. Existing complete ranges already use efficient range reads, so target cold/missing-data scenarios.

**Validate:** command counts and provider calls for 0/1/60 missing dates, direct/inverse/USD paths, rate limits, cancellation, publication windows, duplicate concurrent inserts, and partial completion. Preserve the 60-date bound and provider fallback semantics unless separately approved.

## Secondary candidates requiring measurements

- **Investment recent-read index:** [InvestmentTransactionConfiguration.cs:32](../../code/FinanceManager.Infrastructure/Persistence/Configurations/InvestmentTransactionConfiguration.cs#L32) defines `AccountId` alone, while recent reads order by `(TradeDate, Id)`. Evaluate `(AccountId, TradeDate, Id)` against actual plans and write overhead. It may help single-account reads; a multi-account global top-N can still require sorting. Currency/bond ordering indexes and price-quote time indexes already exist, so do not add duplicate indexes mechanically.
- **Repeated chart input work:** [InvestmentChartRequestLoader.cs:23](../../code/FinanceManager.Components/Features/FinancialAccounts/Services/InvestmentChartRequestLoader.cs#L23) launches five independent requests. Value and benchmark services can separately reload transactions and the same price/FX ranges. The price provider prevents duplicate provider fetches with a listing lock, but it does not cache the entire converted series. Consider a narrow shared chart read model or carefully invalidated series cache only after measuring overlap; preserve independent transaction first paint.
- **Snapshot comparison allocations:** [JsonContentComparer.cs:20](../../code/FinanceManager.Components/Shared/Services/JsonContentComparer.cs#L20) serializes both models to compare them. For large chart snapshots, a typed comparer may avoid large transient strings. Measure first; the coordinator already skips unchanged writes and rendering.
- **Price fetch lock retention:** `InvestmentPriceProvider` retains a static semaphore per encountered listing. Monitor distinct key growth before changing lock lifecycle; naïve removal can allow two locks for the same listing and break deduplication.

## Architecture recommendation

Keep the existing layered modular monolith. The most beneficial approach is narrower repository read models, batched writes, and shared pure valuation inputs within a request. This directly addresses the observed waste while retaining Domain isolation, existing deployment, and both database providers.

A materialized daily-valuation read model becomes preferable if long-history reads remain expensive after O1/O6 and read volume greatly exceeds mutations. Its trade-off is correction/backfill and freshness complexity. Cross-request series caching is a smaller alternative for repeat identical ranges but needs price, transaction, currency, and bond-definition invalidation. Separate-context parallel queries can improve latency when genuinely independent queries dominate, at the cost of more connections and potentially inconsistent snapshots; do not parallelize the current shared-context services. [EF Core documents that a DbContext does not support concurrent operations](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues).

## Suggested implementation and measurement sequence

1. Establish baselines for cold/warm dashboard, investment account, bond history, and import workloads. Record p50/p95 duration, SQL count/time/rows, provider calls, allocated bytes, response size, and cache hit rate. Use synthetic data and controlled providers.
2. Implement O1 and O2 as separate changes: they have direct algorithmic or persistence amplification. Add output-equivalence and failure-path regression coverage.
3. Implement O3, O4, and O5 to remove avoidable requests/reads. Treat the range-cache provenance issue explicitly.
4. Re-measure, then select O6–O10 by observed workload. Do not introduce all suggested caches and read models at once.

Useful existing suites include `BondAccountTests`, `BondAccountEntryTests`, `CurrencyAccountImportServiceTests`, `TransactionLogServiceTests`, `CachedCurrencyExchangeServiceTests`, `InvestmentValuationServiceTests`, `InvestmentPriceProviderTests`, and `DashboardQueryServiceTests`. Relational performance checks must supplement these unit/InMemory tests. No builds or test suites were run for this documentation-only audit; no runtime performance claims are made.

Existing optimizations deliberately retained in this assessment: batched account/boundary reads, request-local account reuse, batched multi-account investment series, positive/negative point FX caches, capped FX provider resolution, price-fetch deduplication/cooldowns, bulk quote upserts, concurrent independent chart requests, stale-refresh gates, and bounded/batched database logging.
