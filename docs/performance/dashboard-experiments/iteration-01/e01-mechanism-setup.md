# E01 mechanism microbenchmark — setup

## File

`code/FinanceManager.Benchmarks/Benchmarks/BondHistoryBenchmarks.cs`
(namespace `FinanceManager.Benchmarks.Benchmarks`, class `BondHistoryBenchmarks`; not an
`ApiBenchmark` subclass — pure domain benchmark, discovered by the existing
`BenchmarkSwitcher.FromAssembly`). No new dependencies; uses existing BDN packages/config.

## Exact fixture (immutable, created in `[GlobalSetup]`)

- Entry: `BondAccountEntry(accountId: 1, entryId: 1, postingDate: 2010-01-01T00:00:00Z, value: 1000m, valueChange: 1000m, bondDetailsId: 1)`
- Window end: `posting + 3650 days` (→ 2019-12-30). No clock dependency anywhere.
- Definition: unitValue 100; one calculation method `DateOperator.UntilDate`, `DateValue = "2030-01-01"`, rate 0.0365 (covers the whole window).
- Params: `[Params(31, 183)] WindowDays`; `[Params(false, true)] Descending`. Dates are exactly `WindowDays` consecutive dates ending at end; reversed when `Descending`.

## Benchmarks

- `RepeatedPointPricing` (`Baseline = true`): sums `entry.GetPriceAt(date, details)` over the window dates.
- `PreparedHistory`: builds `entry.GetPrice(end, details)` **inside every invocation** (full preparation cost included; no global cached history), then sums `history.GetValueOrDefault(date)`.
- Both return a `decimal` checksum. `[GlobalSetup]` verifies each date's point value against the full-history lookup (throws on mismatch) and asserts both methods' checksums are equal. No timing assertions.

## Scope caveat

This measures the **domain computation mechanism only** — repeated per-date history recomputation vs one prepared dictionary of values. It does NOT measure request-context dictionary overhead, API latency, or page performance; there is no host/service/request context in this benchmark. (Stated in the class XML comment.) The file references only stable domain APIs (`GetPriceAt`, `GetPrice`), so it compiles with or without the E01 candidate code — it does not reference `PreparePriceHistory`.

## Results (setup verification only — no timings run)

| Command | Exit |
| --- | --- |
| `dotnet build code/FinanceManager.Benchmarks/FinanceManager.Benchmarks.csproj -c Release` (root) | 0 (0 warnings, 0 errors) |
| `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 dotnet test --project FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj` (code/) | 0 — passed: 2255 / failed: 0 / skipped: 0 |

Logs: `raw/e01-mechanism-build.log`, `raw/e01-mechanism-tests.log`.

Stopped here by design: no commit, no benchmark run. Timings to be captured in a later step.
