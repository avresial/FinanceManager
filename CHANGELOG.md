# Changelog

All notable changes to FinanceManager are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
CalVer (`YY.M.D`) stamped at build time.

See [`.claude/skills/changelog/SKILL.md`](./.claude/skills/changelog/SKILL.md) for the
rules agents must follow when updating this file.

## [Unreleased]

### Added
- Registration now asks for your first name (required) and last name (optional), and the app greets you by your first name in the account menu. #301
- Dashboard cards now surface data-load failures instead of failing silently: a failed fetch shows an error toast (and a "Failed to load" indicator on the net worth, net cash flow, and closing balance cards) rather than leaving an empty card, and a top-level error boundary catches unexpected page errors with a friendly message. The API also now returns consistent RFC-7807 `ProblemDetails` responses for unhandled errors, without leaking stack traces in production. #275
- Production-safe health endpoints: `/alive` (liveness) and `/health` (readiness, including a database connectivity check) now respond in every environment without exposing internal diagnostics, plus an authenticated `/health/detail` endpoint with a full per-check JSON breakdown for operators. #279
- Sessions now persist across page reloads and browser restarts: signing in keeps you logged in for up to 14 days without re-entering your password, access tokens are refreshed transparently in the background, and when the session finally expires you're returned to the login page with a "Your session has expired, please sign in again." message. #226
- Asset diversification card now has a "Show holdings" view that lists your current holdings grouped by asset class — stock tickers, bond names, and cash — loaded on demand when you expand the card. #264
- Admin log viewer: warnings and errors emitted by the API are now persisted to the database and surfaced in the admin panel as a "Recent warnings & errors" dashboard widget and a full `/Admin/Logs` page with warning/error filtering and pagination. A retention background service purges entries older than the configured cutoff (default 30 days) on API start and once a day, and a SignalR hub pushes new entries to the UI live. #212
- Guest demo account now seeds three sample financial insights so the dashboard Insights card is populated instead of empty when trying out the app. #259

### Changed
- Admin AI providers page tidied up: the "Add provider" control is now a dropdown menu (no separate picker), provider cards stay uniform height with a collapsible "Models (N)" section that lists configured models and adds new ones, GitHub now accepts an API key, edits apply on each keystroke and the per-card Save button only appears once something changes. In the Default fallback strategy the model is now picked from a dropdown of the provider's models (no free-text), a new entry is added from a row beneath the table, and Save only appears after a change. #321
- Time-series dashboard cards (assets value over time, net worth, liabilities, investment type) redesigned into a shared card with a live value/delta readout header and a gradient area chart that now shows axes, gridlines, compact currency Y labels, date X labels, and an interactive hover tooltip that syncs the header to the hovered point; the investment-type card also drops its raw Bootstrap markup for `MudCard`. #284
- Net cash flow and closing balance dashboard cards now use the same shared time-series card as net worth over time: a value readout header over a gradient area chart with axes, gridlines, and a hover tooltip, replacing the bare "big number + small line chart" layout. Net cash flow keeps its period total as the hero figure (hovering reveals each month's flow); closing balance shows the latest balance with a trend chip. #283
- Sign-in and registration now use your email address as your login instead of a separate username. #301
- Asset diversification card on the Assets page redesigned from a single radial gauge into a "gauge + breakdown" layout: the score gauge now sits beside a "Built from" bar showing how the asset-class and holdings sub-scores combine, with a Limited/Moderate/Broad band legend and a gentle, band-tinted insight callout (no longer an alarming red warning). #262
- "Assets per type" and "Assets per wallet" cards merged into a single "Assets distribution" card with a segmented "By type / By wallet" toggle that swaps the pie chart in place using the chart library's built-in animation. #268
- "Liabilities per type" and "Liabilities per wallet" cards merged into a single "Liabilities distribution" card that mirrors the Assets distribution card: a segmented "By type / By account" toggle, an ApexCharts pie with a currency tooltip, and a custom scrolling legend (colour dot, name, amount, percentage). #288
- Expense distribution dashboard card modernized to match the Assets distribution card: the `MudChart` pie + plain legend is replaced with an ApexCharts pie (transparent background, slices sorted descending, currency tooltip) beside a custom scrolling legend (colour dot, category name, amount, percentage). #287
- Recurring transactions card tightened: reduced dead space between the header and the transaction list, denser list rows, and the details view now shows the transaction title inline with the back-arrow button instead of stacked below it. #253
- Investment paycheck card on the Assets page redesigned for visual parity with the neighbouring pie cards: hero monthly figure with amber affixes, a clickable replacement-% pill, a continuous 2–8% withdrawal-rate slider paired with Conservative/Standard/Aggressive preset chips (default now 4% to match the safe withdrawal rate convention), a clearer footer surfacing the investments value and salary-history coverage, and an "i" badge that opens a definitions popover. #227
- Investment rate card on the Assets page now surfaces a 12-month rate history bar chart (with the current month highlighted) and an end-of-year projection in PLN derived from the YTD pace, alongside the existing rate, salary, and investments-change figures. #229
- Currency account details page redesigned around a hero (avatar, name, balance, range toggle, chart), a search/filter toolbar with income/expense chips and category picker, day-grouped transaction cards with running balance, and an insight rail (balance change, top 5 income, bottom 5 expenses). #175
- Stock account details page redesigned to mirror the currency layout: a hero (avatar, name, portfolio value in account currency, 1M/3M/6M/1Y/RANGE toggle, chart), a search/filter toolbar with income/expense chips and category picker, day-grouped transaction cards (ticker title, per-entry unit change and position), and an insight rail (balance change, top/bottom movers) that collapses into a slide-in drawer on mobile. #232
- Bond account details page redesigned to mirror the currency layout: a hero (avatar, name, portfolio value in account currency, 1M/3M/6M/1Y/RANGE toggle, chart), a search/filter toolbar with income/expense chips and category picker, day-grouped transaction cards (bond name title, per-entry unit change and position), and an insight rail (balance change, top/bottom movers) that collapses into a slide-in drawer on mobile. #231
- Stock account entry rows now show the ticker symbol in the row title and move the ISIN into the expanded section so the visible identifier is the one users recognize. #224
- Guest demo data now generates realistic per-account-type entries: currency labels respect income vs expense sign, stock and bond holdings never go negative, and stock prices are prefilled across the seeded range. #206
- Settings page redesigned as a single-page, TOC-style layout with Profile, Password, Subscription, and Danger zone sections and a sticky save bar. #209
- Recurring transactions details UI improved: year now shows as small muted annotation at top, table dates simplified to MM-dd format, and long descriptions are trimmed with tooltips to maximize space utilization. #213
- Mobile navigation drawer is now hidden by default and slides in as a temporary overlay when the app-bar hamburger is tapped, freeing horizontal space for page content; desktop keeps the existing mini-rail behavior. #216
- Currency account details page tightened on mobile: the hero hides the avatar, balance, balance-change blocks, and the account-type/currency line (range toggle, account name, and chart stay), and transaction rows drop the avatar and inline `HH:mm` timestamp; desktop layout unchanged. #238
- Account history toolbar reorganized into two clean rows: the search field shares the top row with the single filled "Add entry" call-to-action pinned beside it, while a segmented All/Income/Expense type filter and the category filter sit on the second row next to one quiet overflow "⋮" menu. The overflow now holds import, export, manage, and — on mobile — insights, leaving "Add entry" as the only accent control. #238
- Account details hero decluttered: on mobile the account name moves into the top app bar to free vertical space, and the time-range selector (1M/3M/6M/1Y plus a calendar icon for a custom range) is now a light, borderless strip centered directly under the chart instead of a boxed toggle crowding the title. Applies to currency, stock, and bond accounts. #238
- Guest cash currency seeder now mirrors a realistic monthly budget: a single 5,000 PLN salary on the 1st, recurring 500 PLN investment on the 3rd, 1,000 PLN rent and 100 PLN utilities on the 4th, and 0–10 randomized everyday purchases per remaining day drawn from a list of plausible merchants, capped so monthly outflows never reach the salary. #240
- Guest stock seeder now buys shares in sync with the cash account's monthly investment: each 3rd-of-the-month investment purchases the matching value of the demo ETF at that day's price, so the holding's value reflects real invested cash plus market movement instead of random unit counts. #240
- Guest loan and bond accounts now move in step with cash: the loan is taken out on the guest's first day with its proceeds credited to cash and repaid in equal monthly instalments shown on both accounts, and the bond account buys a fixed amount each month funded by a matching cash outflow. Investment- and loan-related cash transactions carry a descriptive label (Investment/Loan) and description so it's clear what each one paid for. #240

### Removed
- Removed the redundant "Total net worth" card from the top of the dashboard; the "Net worth over time" card below it already shows the same current value alongside its chart. #319

### Fixed
- Registration and sign-in no longer fail intermittently on the production (PostgreSQL/Supabase) database: password hashes were rendered as a raw byte string that could contain a NUL (`0x00`) byte, which SQL Server tolerated but PostgreSQL rejects in text columns, so roughly one in eight accounts could not be created or validated. Password hashes are now stored as hex, which is always valid text. #185
- Expired sessions no longer trap you in a redirect loop between the login and add-account pages (most visible on the guest demo, whose token can't be refreshed): when a token refresh fails the app now fully signs you out instead of only clearing browser storage, so you land on the login page with the "session expired" message rather than bouncing back into the app and flooding the console with 401 errors. The user-greeting lookup in the main layout also no longer throws an unhandled exception on an expired token. #322
- Landing page no longer crashes when the visit-tracking call fails: a failed `PUT /api/NewVisitors` (e.g. a 500) is now caught and logged instead of throwing out of the page's initialization, so the landing/demo page always renders. #316
- Time-series dashboard cards (assets value over time, net worth, liabilities, investment type) no longer render the gridline labels with a heavy bold halo, and the date/month annotation along the bottom axis — previously clipped out of view — is now visible. #313
- Assets distribution card on the dashboard no longer renders an oversized pie that hid the legend: the chart is now a fixed size so the legend always has room to show each type/wallet name beside its amount and percentage, the stray horizontal scrollbar is gone, and the chart no longer occasionally draws tiny after re-rendering. #292
- Expense distribution card on the dashboard is no longer empty on the guest demo (and any fresh install): the default financial labels now seed a sensible spending-necessity classification (e.g. Groceries/Rent/Utilities as Essential, Entertainment/Dining Out/Travel as Want, Investment as Investment), so expense transactions are categorised and charted instead of being silently dropped. Labels an admin has already classified are left untouched. #271
- Investment paycheck card no longer flashes into its loading spinner when you drag the withdrawal-rate slider or pick a preset: the monthly figure is recomputed instantly from the already-loaded estimate, so it now updates smoothly instead of briefly showing the value, blanking to a spinner, then redisplaying the same value. #227
- Investment paycheck card's top-right "i" info badge now opens its definitions popover when clicked — the activator icon button was swallowing the click so the tooltip never appeared. #227
- Investment rate card layout fixed: the separator between the chart and the Salary/Investments footer no longer renders as a tall grey rectangle (it is now a thin line), the bar chart fills the available middle space, and the footer is pinned to the bottom of the card. #255
- Expandable transaction rows on the currency, stock, and bond account details pages are now keyboard-accessible — each row exposes a button role and toggles open/closed on Enter or Space. #247
- Guest stock account no longer shows "Stock price unavailable" on every entry — the demo seeder now stores a real ISIN and a matching `StockDetails` row so the ticker resolves from the local sandbox instead of falling through to OpenFIGI. #222
- Guest dashboard's net cash flow card no longer times out — stock price lookups now preload in bulk per ticker instead of one external resolve per entry. #208
- Dashboard and asset pages no longer make an external OpenFIGI request for every stock price lookup — ticker→ISIN resolution is now cached in memory and served from local `StockDetails` first, with OpenFIGI as last-resort fallback.
- Settings page no longer overflows on mobile — profile row, danger zone, and the unsaved-changes bar reflow vertically on narrow viewports while the desktop layout stays unchanged. #218

### Security
- Repeated failed logins now lock the targeted account: after 5 consecutive wrong-password attempts the account is locked for 15 minutes (both configurable under the `AccountLockout` section), the counter is persisted per account and resets on the next successful sign-in, and a locked account returns a clear, non-enumerating message. This complements the per-IP rate limiting by stopping a brute-force that rotates source addresses against a single account. #277
- API requests are now rate limited: a lenient global limit guards every endpoint, with tighter limits on authentication endpoints (login, refresh, logout) to blunt brute-force/credential-stuffing and on stock endpoints that fan out to paid external providers (Alpha Vantage, OpenFIGI) to prevent quota exhaustion. Exceeding a limit returns `429 Too Many Requests` with a `Retry-After` header. Limits are configurable under the `RateLimiting` section and partitioned per authenticated user (falling back to client IP for anonymous traffic). #276
- The JWT signing key is no longer committed in `appsettings.json`; it must now be supplied via environment variable / User Secrets and the API refuses to start without it outside Development. The previously committed key has been treated as compromised and removed, so all existing access tokens are invalidated and users must sign in again. #273

## [26.5.25] - 2026-05-25

### Added
- Automated CalVer (`YY.M.D`) versioning stamped at build time. #183
- Auto-deploy of the `develop` branch to the dev Azure Web App. #189
- Diversification proxy gauge widget on the assets page. #130
- Recurring transactions card with drill-down on the dashboard. #179
- Net-worth time-series card on the dashboard and assets page. #149
- Date-range filter on bond, currency, and stock account transaction lists. #19
- AI provider management tab in the admin panel, including provider fallback chain configuration. #169
- LM Studio support as an AI provider.
- Outlier sub-score explanations on the diversification card. #173

### Changed
- Diversification card visualization switched to an ApexCharts radial-bar gauge with chip explanations. #173
- Diversification meter moved from the dashboard to the assets page. #166
- Bond account details rows redesigned to match the stock display pattern. #174
- Full README rewrite. #170
- Recurring-transaction grouping now uses similarity matching across months.
- Performance: net-worth range queries now preload once instead of per-day refetch. #167

### Fixed
- Diversification score now uses current positions rather than full account history. #164
- Carry-over stock tickers are included in range valuation. #167
- Guest user data is no longer persisted in the main application database. #171
- Bond CSV export now contains a `Bond` column (previously `BondDetailsId`). #187
- Filter chip click on account details filter bars.
- Net-worth chart clears stale data on null-user and failed-fetch paths.
- `To`-only date filter respects end-of-day boundary.

## [26.4.29] - 2026-04-29

### Added
- Currency account import with real-time progress via SignalR.
- Bond account import.
- Stock-entry import with conflict resolution.
- CSV export for bond, currency, and stock accounts, including shares count and price on stock exports.
- Currency exchange-rate providers with caching.
- Skeleton loading states for charts and side panels on account details pages.
- Recent-entries loader for historical account entries on stock account details.

### Changed
- Stock price retrieval now supports currency conversion and uses optimized price-series indexing.
- Account export DTOs refactored for consistency across asset types.

### Fixed
- Null-result handling in bond, currency, and stock import components.

## [26.3.17] - 2026-03-17

### Added
- Investment Paycheck Estimator with safe-withdrawal-rate inputs. #128
- Essentials spending calculator and financial label classifications. #127
- Distribution-of-expense card on the dashboard. #126
- Dedicated balance services for currency, bond, and stock accounts, enabling per-asset-type cash-flow and closing-balance reporting.

### Changed
- Investment Paycheck Estimator excludes the current partial month from salary average for more accurate projections.
- Refined transaction categorization prompt and financial labels.

## [26.2.28] - 2026-02-28

### Added
- AI-generated financial insights with background generation and prompt provider. #122
- AI label assignment for currency entries, with batching, deduplication, and a "no match" sentinel for unlabelled entries.
- Label-setter progress tracking surfaced through API and UI components.
- Bond management: issuer retrieval, bond details, and admin UI.
- Confirm-delete dialog for stocks.
- Value-change range filter on bond, currency, and stock account details.
- Search, breadcrumb navigation, and refreshed UI on admin pages.
- `ContractorDetails` field on currency entries, optional during import.
- CSV header mapping configurable per user, with admin UI and API endpoints.
- `Investment` and `Undisclosed Income` defaults added to the standard label seeder.

### Changed
- AI provider stack migrated to `Microsoft.Extensions.AI` with a configurable fallback chain.
- GitHub Models provider switched to `GitHub.Copilot.SDK` via `CopilotChatClient`.
- Account command surface unified around `AddAccount` across bond, currency, and stock flows.

### Fixed
- CORS configuration for cross-origin requests in development.
- Stock price provider robustness with null checks and improved currency-exchange resource handling.

## [26.1.18] - 2026-01-18

### Changed
- "Bank account" renamed to "Cash account" across the UI, API, and storage. #116
- Account and stock-entry naming refactored for clarity across components and services.
