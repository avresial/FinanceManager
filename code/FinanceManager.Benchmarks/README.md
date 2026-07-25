# FinanceManager.Benchmarks

Measures the per-request cost of FinanceManager's most-used API endpoints, so an optimization refactor
has a baseline to be judged against.

The suite boots the **real API** — full middleware pipeline, JWT auth, controllers, application and
domain services, repositories, EF Core — against a **local SQLite file** seeded with the same six months
of demo data the in-app guest sandbox generates.

## Quick start

```bash
cd code/FinanceManager.Benchmarks

# 1. Check every benchmarked endpoint still returns real data (seconds, not minutes).
dotnet run -c Release -- --validate

# 2. Run the whole suite (~6 minutes).
dotnet run -c Release

# Or one group at a time.
dotnet run -c Release -- --filter '*Dashboard*'
dotnet run -c Release -- --filter '*MoneyFlow*'
```

Results land in `BenchmarkDotNet.Artifacts/results/` as GitHub-flavoured markdown (paste into a PR) and
full JSON (diff two runs programmatically).

**Always run `--validate` first.** A benchmark that quietly measures a `403` or an empty result still
produces a tidy table of numbers, which is the worst possible failure mode for a baseline. Validation
runs the same calls the suite does — discovered by reflection, so it cannot drift out of sync — and
prints the status and response size of each.

## What is covered

Read-heavy endpoints that sit on the critical path of a page load or a chart interaction, grouped by
BenchmarkDotNet category:

| Category | Endpoints | Why |
|---|---|---|
| `Dashboard` | `Dashboard/overview` over a 6-month and a 1-month window | The single most important number: what the landing page waits on after login. Two windows separate fixed overhead from cost that scales with range. |
| `MoneyFlow` | net worth (point and series), inflow, outflow, net cash flow, closing balance, labels value, investment rate | Re-fired every time a user changes the date range; felt as chart lag. |
| `Assets` | asset existence, end assets per account and per type, time series, paycheck estimate, unrealized gain/loss per account and per instrument | The heaviest reads in the app — transactions joined to listings joined to daily price quotes, then currency-converted. |
| `Analysis` | expense distribution, essential spending, diversification score and breakdown, liabilities | One per dashboard card. |
| `Accounts` | currency/investment/bond account lists and details, full-range vs paged entry reads, holdings and valuation | Drill-down pages. The range-vs-page contrast shows whether paging is actually saving the user anything. |
| `Supporting` | user record, currency list, label count/page/by-account, recent transaction log | Called from layout and navigation, so their cost lands on *every* interaction. |
| `Writes` | append a currency entry, recalculate account balances | A slow write is felt directly, and an over-broad cache invalidation makes the *next* read slow too. |

### Deliberately excluded

- **Import and export** — as requested. Bulk, user-initiated, and not on any interactive path.
- **Login** (`POST api/Login`). Its cost is dominated by password hashing, which is *supposed* to be
  slow and must not be "optimized". Including it would put a large, deliberately-fixed number next to
  numbers that are meant to move.
- **Admin, AI insight generation, label setting, price backfill, OAuth/MCP** — background or
  low-frequency work, not interactive latency.

## Reading the numbers

`Allocated` is the most portable column — allocation per request barely varies across machines and is
usually the fastest route to a real improvement. Wall time is only meaningful *relative to another run
on the same machine*.

`P95` is reported next to `Mean` because a smoother UX is mostly about the slow requests.

The `Writes` category carries wider error bars than the reads by construction. Resetting state between
invocations forces BenchmarkDotNet down to one invocation per iteration, so each measurement is a single
request rather than an average over many. Compare write numbers across runs using `Median` and `P95`, and
treat a change smaller than the error column as noise.

## What is and is not measured

**In scope:** routing, model binding, JWT validation, authorization, the controller, application and
domain services, the repository layer, EF Core query translation and execution against SQLite, and JSON
serialization of the whole response body (every benchmark reads the response to completion, so a change
to a response DTO's shape cannot look free).

**Out of scope:** Kestrel, sockets and TLS. `WebApplicationFactory` serves requests over an in-memory
transport. This is deliberate — the excluded work is a near-constant per-request cost that a service or
query refactor will not change, and excluding it removes the largest source of run-to-run variance.
Read the numbers as *server-side work per request*, not as end-to-end wire latency.

**Also out of scope:** concurrency. Endpoints are measured one at a time, so nothing here says anything
about contention between the dashboard's parallel card requests.

### SQLite caveats

Absolute numbers do not transfer to production hardware — the point is comparing *before* against
*after* on the same machine, not capacity planning. Two provider-specific differences are worth knowing:

- **`decimal` is stored as TEXT.** SQLite has no decimal type, so EF Core stores money as text and
  aggregates coerce it to floating point. Timings are representative; the last cents of a computed
  total may not be.
- **`DateTimeOffset` is stored as an order-preserving integer.** EF Core's SQLite provider refuses to
  translate `DateTimeOffset` comparisons at all, and price quotes are filtered exactly that way. See
  `SqliteModelCustomizer`.

## Environment variables

| Variable | Default | Effect |
|---|---|---|
| `FM_BENCH_COLD_CACHE` | off | Replaces `HybridCache` with a pass-through, forcing every request down to SQLite. See below. |
| `FM_BENCH_RESEED` | off | Discards the seed template and generates a fresh dataset. |
| `FM_BENCH_SEED_MONTHS` | `6` | Months of demo history to seed. |
| `FM_BENCH_DATA_DIR` | `<bin>/benchmark-data` | Where the seed template and working copy live. |
| `FM_BENCH_LOG_LEVEL` | `Warning` | Raise to `Information` to see EF Core's SQL when diagnosing a failure. |
| `FM_BENCH_OUT_OF_PROCESS` | off | Use BenchmarkDotNet's default per-benchmark child processes instead of the in-process toolchain. |

### Warm vs cold cache — run both

```bash
dotnet run -c Release                          # warm (default)
FM_BENCH_COLD_CACHE=1 dotnet run -c Release    # cold
```

BenchmarkDotNet invokes each endpoint thousands of times, so a real cache is warm from the second
invocation onwards and the reported time collapses towards "serialize an already-computed result". That
is a legitimate number — it is what a user clicking around a warm dashboard sees — but it hides the
repository and EF Core cost that a read-path refactor actually targets.

**The gap between the two runs is the headroom.** A large gap means the cache is carrying the endpoint
and the underlying query is slow; a small gap means the cache is not helping and the cost is real work
on every request.

The first run of this suite makes the point better than any explanation. `Dashboard/overview` over six
months, same machine, same data:

| | Mean | Allocated |
|---|---:|---:|
| Warm cache | 0.80 ms | 120 KB |
| Cold cache | 470 ms | 16.6 MB |

`HybridCache` is carrying that endpoint almost entirely. The number a user actually waits on is the cold
one — it is paid on the first dashboard load of a session, and again after every write that invalidates
the user's cached read models. Also worth noting from that first run: `Assets/IsAnyAccountWithAssets`
returns a single boolean and costs ~13 ms, the same order as endpoints that return a full breakdown.

## How the harness works

1. **`BenchmarkDatabase`** builds a pristine seeded SQLite file *once* (`seed-template.sqlite`) and
   copies it to a per-process working file for each run. The template is reused, so a baseline run and a
   post-refactor run measure byte-identical data; the copy means write benchmarks can mutate freely
   without contaminating the next run. Seeding writes to a staging file and moves it into place only on
   success, so an interrupted seed cannot leave a half-populated template behind.
2. **`BenchmarkApiHost`** boots the API against that file. It replaces the `AppDbContext` registration
   with SQLite (and asserts the swap took, so a silent fallback to the in-memory provider cannot pass
   unnoticed), removes FinanceManager's hosted services, disables rate limiting and startup backfill,
   and swaps the currency-rate and stock-price providers for offline stand-ins so no measured iteration
   can turn into an HTTP call to a rate-limited vendor.
3. **`BenchmarkEnvironment`** is a process-wide singleton holding the host, an authenticated
   `HttpClient`, and the resolved `BenchmarkScenario`.
4. **`BenchmarkScenario`** carries the account ids and date window, *resolved from the running API*
   rather than hard-coded — the seeders anchor history to `UtcNow`, so a hard-coded window would drift
   into empty ranges as the template ages and endpoints would quietly start measuring "return nothing".

Adding an endpoint: add a `[Benchmark]` method to the matching class (or a new `ApiBenchmark` subclass),
call it from `Prime()` to keep first-call costs out of the measurement, and re-run `--validate`.

## Note on `CurrencyEntryValueCalculator`

Running-balance recalculation is a hand-written windowed `UPDATE` with no portable spelling. Before this
project it had a PostgreSQL branch and an `else` branch that assumed SQL Server syntax, so *any* other
relational provider got statements it could not parse. Adding SQLite support meant making that switch
explicit (`DatabaseProviders`), which also means an unrecognised provider now falls back to the correct
managed path instead of failing. Production behaviour on SQL Server and PostgreSQL is unchanged.
