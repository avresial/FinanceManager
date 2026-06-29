# Changelog

All notable changes to FinanceManager are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
CalVer (`YY.M.D`) stamped at build time.

See [`.claude/skills/changelog/SKILL.md`](./.claude/skills/changelog/SKILL.md) for the
rules agents must follow when updating this file.

## [Unreleased]

### Security
- The default admin and test-user accounts are no longer seeded with passwords hard-coded in source. Their passwords are now read from configuration (`Seeding:AdminPassword` / `Seeding:TestUserPassword`); when unset — as in production — the accounts are not created. The stock-price bulk-import endpoint now returns a generic error message and logs the exception server-side instead of echoing the raw exception text to the caller. #450

### Added
- Instrument discovery now has a search API (`GET /api/investments/instruments/search?query=`): typing a ticker, name, or ISIN returns listing-level matches assembled from OpenFIGI (canonical identity — FIGI, ISIN, exchange MIC/name, trading currency) and Alpha Vantage (price-symbol candidate). Results are merged and de-duplicated, carry a source (`OpenFIGI`/`AlphaVantage`/`Combined`) and a confidence score, and surface warnings for ambiguous exchange mappings, minor-unit (e.g. GBX) quoting, missing price symbols, and currency mismatches. Results are cached for 15 minutes to stay within provider rate limits. This is the first increment of the discovery feature; import-preview/import and the search-and-import UI follow in later changes. #510
- The investment account details view now matches currency and bond accounts: a value-over-time area chart with a Month / 1M / 3M / 6M / YTD time-range picker (plus a custom range), a search-and-filter toolbar (search trades by ticker, exchange or notes, and filter by Buy/Sell), a balance-change card, and a "Top movers" widget ranking the in-range trades by cash impact. The Holdings panel moves alongside the new insight cards (into a slide-out drawer on mobile), and the transaction list is restricted to the selected range. #508
- The "Add / Edit transaction" form in investment accounts now searches instruments by typing — a live autocomplete queries all asset listings in the database and shows matching ticker and exchange. Currency and unit price are no longer free-text inputs: they are auto-filled from the selected listing's trading currency and latest stored price quote, and cannot be overridden. #506
- Adding a stock holding now needs only a search term, units, and a date: the form resolves the instrument through the `search-instrument` endpoint and auto-fills name, type, currency, and price. Unambiguous matches resolve silently; when several listings match, an inline picker (name · exchange · currency · ISIN) lets you choose. The confirmed instrument's ISIN is stored as the entry key and the typed term is kept as the display ticker. Entries still save when a price can't be fetched, showing a subtle "price pending" indicator so a provider outage doesn't block the add. #434
- Dashboard data is now cached in the browser's local storage: navigating back to the dashboard after visiting another page renders the previous result instantly, then silently refreshes in the background when the server responds. #444
- Admins get a new **Assets** management screen (Admin → Assets) for the investment asset model: create, search, edit and delete assets, their per-exchange listings (ticker, MIC, trading currency, price multiplier, primary/active flags), and the provider-specific market data symbols on each listing. Backed by a new admin-only `api/admin/assets` API. #477
- Investment accounts now have a dedicated details view on the new asset model: opening an account that holds investment transactions shows current holdings (per exchange listing, with units, price and value), the total holdings value, and a Buy/Sell transaction history, and lets you add, edit and remove transactions inline. The guest demo includes a seeded investment account (iShares Core S&P 500) so the view is populated out of the box. Accounts on the legacy stock model are unaffected. #490

### Fixed
- Adding a stock by ticker now resolves and prices reliably: instrument identity is keyed on the share-class FIGI that OpenFIGI actually returns, instead of an ISIN it never returns for a ticker lookup. Unambiguous ticker searches now auto-resolve and save (and fetch prices) even when no ISIN is available, with ISIN kept as an optional cross-reference. #473
- The dashboard no longer blanks out if reading its saved snapshot fails: a local-storage or interop error on the snapshot read is now logged and the page falls through to a fresh server fetch instead of aborting the load. A slower, superseded reload also no longer overwrites the snapshot written by a newer one, preventing a stale first paint on the next visit. #463
- The custom date-range icon in the account-details range selector now works: clicking the calendar icon opens a date-range picker (previously it was embedded in a menu and never appeared), letting users pick an arbitrary start/end range for the account history and chart. #436
- The next-younger boundary entry attached to a loaded account (used to extend balance series past the selected window) is now resolved from the end of the date range instead of the start, so it correctly points at the first entry *after* the window rather than the earliest in-range entry. Affects both single-account and whole-portfolio loads. #411

### Changed
- The **Admin → Logs** page is now denser and mobile-friendly: each entry is a compact card whose header (log-level chip, timestamp, category) and message stay visible at all times, instead of a table that pushed metadata off-screen on narrow viewports. Stack traces are collapsed by default behind a **Show details / Hide details** toggle and, when expanded, render in a bounded scrollable monospace block that wraps long lines so they never push the card past the screen edge. The log-level filter shows icon-only buttons (with accessible labels) on mobile and keeps text labels on desktop. #511
- The investment account details view now shows its transaction history in the same style as currency and bond accounts: trades are grouped into per-day cards (with the day's trade count and net cash impact), and each transaction is a compact, clickable row that expands to reveal full details (trade date, type, ticker, exchange, quantity, unit price, gross value, fee, cash impact, currency, notes) along with the edit and delete actions — which are no longer always visible in the row. Holdings move to a secondary side card so the transaction history is the main content. #503
- Stock accounts now run entirely on the investment asset model — the legacy `Stock` model has been removed end to end, completing the stock → investment cutover. Stock-type accounts open in the investment holdings/transactions view, are created and managed through the investment account API/UI, and are valued solely through the investment valuation service. The legacy per-ticker stock-entry path, the standalone stock-price store, and their admin tooling are gone: the **Admin → Stocks** screens (list, add, edit, bulk price import) and the **Market data → Stock prices** page have been removed (admins manage instruments under **Admin → Assets** instead), along with the stock-price/stock-account/stock-entry/stock-import APIs and their HTTP clients. A database migration drops the now-unused `StockEntries`, `StockPrices`, and `StockDetails` tables (destructive and irreversible — non-stock history is preserved). Renaming and deleting an investment (formerly stock-type) account is unchanged. #496
- The dashboard's closing-balance and assets-distribution cards now include investment accounts on the new asset model. A new investment balance service values each investment account's holdings through the investment valuation service and feeds the dashboard aggregates, while reporting nothing for cash-flow (inflow/outflow/net cash flow) so the money-flow cards are not double-counted against the cash account that books the investment outflow. Legacy stock accounts remain valued once by the stock balance service. #495
- Net worth and the diversification score/breakdown now include investment accounts on the new asset model. Investment-account holdings are valued through the investment valuation service and added to net worth (point-in-time and over time) without double-counting legacy stock accounts, and their listings are counted under the "Stocks" class in diversification. #493
- Stock prices are now fetched through a provider fallback chain and use split/dividend-adjusted data. Daily series come from Alpha Vantage's adjusted endpoint first and fall back to EODHD when Alpha Vantage is rate-limited, unentitled, or has no data — so a single provider hitting its free-tier cap no longer leaves prices blank. Using the adjusted close also keeps historical returns and charts correct across stock splits and dividends. Admins can set the EODHD key under Service Keys. #469
- Account details pages now actually open instantly on re-navigation: the account-type lookup that ran on every visit (three uncached account-list requests, shown behind a full-page spinner before the saved snapshot could paint) is now cached in the browser. The cache refreshes when accounts are added or removed and when you log in or out, so it never serves another user's data. #467
- Account details pages (currency, stock, and bond) now paint instantly on re-navigation: the initial transaction history is saved to a per-user, per-account local-storage snapshot and rendered immediately, then reconciled against a fresh server fetch and re-saved only when the entries actually differ. The balance chart continues to load from the API on every visit and is never snapshotted. #465
- The dashboard's instant-paint cache now runs through a standardized, reusable UI snapshot service backed by local storage. The dashboard keeps a single snapshot per user (no date in the key, so it is overwritten on each load) and only repaints and re-saves when the freshly fetched data actually differs from what is already shown. #461
- The admin "Add stock" screen now works search-first: type a ticker or ISIN, hit Search, and the form auto-fills name, type, region, currency, ISIN and Alpha Vantage symbol from the `search-instrument` resolver. Ambiguous matches show an inline picker; no-match falls back to manual entry. The reviewed details are saved with the already-resolved ISIN, so adding no longer fails with "Could not resolve ticker to ISIN" after typing every field by hand. #459
- Account entry range reads (used by the account chart and dashboard) are now served from per-user, calendar-month bucket cache. On a cache miss the server fetches the full calendar month so any subsequent request touching that month — whether a different slice or the next dashboard load — requires zero database queries. Only the rolling 12 most-recent months are eligible; older data is fetched directly. Any write still invalidates the owner's cache immediately. #456
- The cheap, frequently-called account-entry point reads (latest entry, first entry, entry count, and posting-date list) are now served from a per-user `HybridCache` across currency, stock, and bond accounts, cutting repeated single-row database queries on common account and dashboard views. Any write to an account's entries (add, edit, delete, import, label) invalidates that user's cached entry and dashboard data together, so balances stay correct after every change. #455
- Dashboard charts that convert stock prices into a different display currency (net worth, closing balance, net cash flow, and the stock account details page) now render faster: per-day FX conversions are prefetched once per currency over the whole range and the exchange-rate cache is actually used, instead of issuing one uncached conversion per day per ticker. #447
- Opening an account's transaction history now loads faster: backfilling the initial view to its minimum entry count no longer re-queries the whole date range once per added entry (an O(n²) pattern). The currency entry provider now resolves the window from a single lightweight posting-date projection and fetches the entries in one further query. The duplicate `GetInitialTransactionHistory` account endpoint was also removed in favour of the equivalent date-range `Get` endpoint. #442
- All read-only data queries (entries, accounts, labels, stock prices) now skip EF Core change-tracking and use split queries where collection joins occur, reducing per-request memory overhead and eliminating Cartesian row duplication on label/classification reads. #408
- Loading a whole user's accounts (`GetAccounts`) now batches its database access: instead of ~5 queries per account — plus an extra query per stock ISIN and per bond — each account type loads in a constant number of queries (accounts, in-range entries, and the next-older/next-younger boundaries), cutting the query volume behind the dashboard and other multi-account reads. #411
- Stock price queries (`Get`, `GetRange`, `Update`, bulk import) now use sargable date predicates so the `(StockIsin, Date)` index is used for seeks instead of full scans; the gap-search look-back is bounded to two years; and bulk import preloads all required data in two queries instead of two per price. #409
- Adding, editing, or deleting a historical transaction on a large account is now significantly faster; the running-balance recalculation is performed as a single database statement instead of one update per row. #412
- Deleting an account with many entries is now significantly faster; the server no longer loads every entry into memory before deleting. #413
- The admin user list now loads its used-record-capacity column with a single grouped count query for the whole page instead of one count query per account per user, and fetches the page of users in one query; the previous code also blocked a thread on a synchronous `.Result` call per row. #414
- Stock account per-ISIN boundary lookups (`GetNextOlder`/`GetNextYounger`), used on the running-balance recalculation hot write path, now resolve every ISIN with one grouped query plus a single fetch instead of one query per ISIN; `GetNextYounger` also gains the missing account filter so it no longer scans every account's entries for the ISIN list. #410

### Added
- New `search-instrument` endpoint on `StockPriceController` that calls `IInstrumentResolver` and returns either an auto-resolved match or a list of candidate listings (ISIN, AlphaVantageSymbol, Name, Exchange, Currency) for the UI to disambiguate. Price endpoints (`get-stock-price`, `get-stock-prices`, `add-stock-price`, `update-stock-price`) now key on ISIN directly, resolving to `StockDetails.AlphaVantageSymbol` when an external provider call is needed; the broker ticker is no longer accepted as a key. `StockPriceHttpClient` updated accordingly with a new `SearchInstrument` method. #433
- ISIN-centric instrument resolver that auto-resolves unambiguous securities or returns candidates for user confirmation, reconciling Alpha Vantage and OpenFIGI data sources; handles broker suffix normalization and GBX/GBP currency conversions with configurable quote factors. #432
- Stock details now store a dedicated Alpha Vantage price symbol separate from the broker display ticker, enabling accurate price resolution when the two differ (e.g. broker shows `CSPX.UK`, Alpha Vantage uses `CSPX.LON`). ISIN is now the authoritative key for stock account entries; the broker ticker is a display-only alias. #431
- "Recalculate balance" action on the account management screen for currency, stock and bond accounts that rebuilds every entry's running balance from its value-change series (per instrument for stock/bond), repairing accounts whose stored balances drifted — e.g. legacy imports where each entry's value equals its value change. #435
- Admin page (`/Admin/ServiceKeys`) for managing external service API credentials (Alpha Vantage, OpenFIGI); keys are persisted in the database and take effect immediately without redeployment. #358

### Changed
- Dashboard first-paint data now loads through a single `/api/Dashboard/overview` request instead of each card fetching its own endpoint: the page fetches one overview model and passes prepared data to the net worth, net cash flow, closing balance, liabilities, financial labels, assets, and expense cards, reducing dashboard load chattiness. Cards still self-load when used standalone outside the dashboard. #398
- Account history toolbar redesigned to match design spec: Income and Expense are now separate toggle buttons (green/red when active), the label filter is renamed to Category, Import/Export/Settings are surfaced as inline toolbar buttons, and the Add entry button is a standalone filled primary button; the toolbar container no longer has an outlined border. #355
- Account hero range selector repositioned to the top-right corner on desktop (same row as account name) and updated to offer 1W, 1M, 3M, 6M, YTD, and All presets; the custom date-range picker has been removed. #355
- Account history toolbar wrapping layout now groups search + type filter together on the first row and label filter + add-entry button together on the second row when the toolbar is too narrow to fit on one line; Income/Expense toggle always shows full text at all viewport widths. #353
- Account history toolbar buttons (Income/Expense toggle, Category filter, Add entry) now render in a muted gray that matches the search input's outlined border, reducing visual noise in the control bar. #355

### Fixed
- Plan record-capacity now counts entries across all account types — currency, stock and bond — instead of currency entries only; this fixes the admin user list usage column and the entry plan-limit checks under-counting usage and letting users exceed their record limit. #424
- Account history toolbar no longer overflows on narrow mobile screens: the Income/Expense toggle items show only their arrow icons on mobile (text hidden), and the label filter button shows only its icon, keeping all controls within a 360 px viewport. #350
- Transaction row Edit/Duplicate/Delete action buttons now share a consistent outlined style; Edit was previously filled amber while the others were outlined. #351

### Changed
- Account transaction toolbar redesigned into a single wrapping row: search field, All/Income/Expense segmented toggle, multi-select label filter, and a split "Add entry" button that keeps Import, Export CSV, and Manage account in an attached dropdown — the whole bar now fits on one line at full width. #289
- Expanded transaction row details now show a running balance field and a Duplicate action alongside Edit and Delete; the actions are styled as filled/outlined buttons; labels are displayed as chips with a "+ Add label" shortcut; a dashed divider and amber row tint signal the open state. #290
- Date-range selector on account pages now includes preset shortcuts (Last 7 days, Last 30 days, Last 3 months, Last 6 months, Year to date) and an explicit Apply/Cancel flow instead of auto-committing on date selection, with a two-month calendar grid for custom ranges. #289
- All dashboard and account charts now use ApexCharts exclusively: the hand-rolled Chart.js interop (`LineChartJs`) used on the account balance hero and stock prices page has been replaced with ApexCharts area charts, and the two MudChart bar charts on the admin dashboard have been replaced with ApexCharts bar charts. The Chart.js, moment.js, and chartjs-adapter-moment scripts have been removed from the bundle. #291

### Fixed
- Dashboard charts (the asset/expense/liability distribution pies in particular) no longer crash the page under the production Content-Security-Policy. ApexCharts evaluates its chart options as JavaScript, which the policy added in #278 blocked, taking down the whole dashboard with a "Something went wrong" error; `'unsafe-eval'` is now permitted in `script-src` so the charts render. #291

### Added
- Forgot-password flow: the sign-in page now has a "Forgot password?" link that opens a page where you enter your email to request a reset link, then a page to choose a new password. Reset links are single-use and expire after an hour. While email delivery isn't set up yet, the reset link is shown on-screen straight after you request it. #280
- New users with no accounts now land on a welcoming first-run screen instead of being dropped straight onto a blank "Add account" form: a short intro to what FinanceManager does, one-click "Create" buttons for a Currency, Stock, or Bond account (each opening the form with that type preselected), and a carousel previewing the dashboard, currency, stock, and assets pages (the screenshots follow your light/dark theme). #281
- Registration now asks for your first name (required) and last name (optional), and the app greets you by your first name in the account menu. #301
- Dashboard cards now surface data-load failures instead of failing silently: a failed fetch shows an error toast (and a "Failed to load" indicator on the net worth, net cash flow, and closing balance cards) rather than leaving an empty card, and a top-level error boundary catches unexpected page errors with a friendly message. The API also now returns consistent RFC-7807 `ProblemDetails` responses for unhandled errors, without leaking stack traces in production. #275
- Production-safe health endpoints: `/alive` (liveness) and `/health` (readiness, including a database connectivity check) now respond in every environment without exposing internal diagnostics, plus an authenticated `/health/detail` endpoint with a full per-check JSON breakdown for operators. #279
- Sessions now persist across page reloads and browser restarts: signing in keeps you logged in for up to 14 days without re-entering your password, access tokens are refreshed transparently in the background, and when the session finally expires you're returned to the login page with a "Your session has expired, please sign in again." message. #226
- Asset diversification card now has a "Show holdings" view that lists your current holdings grouped by asset class — stock tickers, bond names, and cash — loaded on demand when you expand the card. #264
- Admin log viewer: warnings and errors emitted by the API are now persisted to the database and surfaced in the admin panel as a "Recent warnings & errors" dashboard widget and a full `/Admin/Logs` page with warning/error filtering and pagination. A retention background service purges entries older than the configured cutoff (default 30 days) on API start and once a day, and a SignalR hub pushes new entries to the UI live. #212
- Guest demo account now seeds three sample financial insights so the dashboard Insights card is populated instead of empty when trying out the app. #259
- Accessibility and automation: every page now sets a browser tab title, icon-only buttons (edit, delete, close, menu, pagination, etc.) carry descriptive labels for screen readers and browser-driving agents, the loading spinner announces itself as busy, and the navigation menus and key flows (sign-in, demo login, add account) expose stable hooks. #337

### Changed
- AI Insights dashboard card redesigned (Direction B): the Generate button is removed (generation is now fully automatic), a segmented amber progress bar at the top of the card auto-advances insights every 6 s and pauses on hover/focus, the header gains a relative timestamp and a subtle "AI" amber pill badge, tag chips move to the carousel footer beside the navigation arrows, the loading state uses skeleton lines instead of a spinner, and the empty state shows a centred amber orb with explanatory copy. #347
- "Money by label" dashboard card (formerly "Labels") redesigned from a plain list into a polished, ranked breakdown: the header shows the reporting period and a sign-coloured net total, with separate Income and Spending groups each carrying a subtotal. Compact rows pair a tinted category icon with the signed amount in tabular figures (a "+" prefix and green for income, red for spending), a proportion bar scaled within its group, and a "% of income/spending" share. Loading shows matching skeleton rows, an empty period shows a centred "No data" prompt, and failures show an inline error with a "Try again" retry. #285
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
- The storage-capacity indicator on the settings page now shows your real usage instead of always appearing empty — the record-capacity lookup was discarding its result and always returning nothing. #360
- Returning to the app with a still-valid session now logs you straight back in instead of bouncing you to the login page: opening the app (or a deep link into any page) within the 14-day window silently restores your session in place, and only a missing or expired session sends you to the login screen. Previously a protected page would sign you out — revoking your refresh token — before the silent refresh could run. #339
- Newly registered accounts can now sign in. Logins (email addresses) are now stored lowercased at registration to match the lowercased lookup performed at sign-in, so an account registered with any uppercase letter in its email is no longer rejected as "incorrect username or password" on the case-sensitive PostgreSQL database. #331
- Passwords changed from the admin "Edit user" page or the user settings page can now be used to sign in. Password hashing was happening inconsistently (twice for registration/login, once for password changes), so a changed password never matched at sign-in. Hashing is now done in exactly one place — the API — for every path, and the seeded admin and test accounts were aligned to the same single-hash format. #331
- The admin "Edit user" change-password form no longer reports "Passwords don't match" when both fields are identical: the first password field wasn't bound to a value, so the match check compared against an empty value. #331
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
- Account access controls hardened: changing your own password now requires entering your current password (verified server-side); a non-admin account can no longer delete or change the password of any account other than its own; and the admin metrics/user-enumeration endpoints now require the Admin role instead of any signed-in user. The API also restricts accepted host headers, adds a `Permissions-Policy` response header disabling camera/microphone/geolocation, and only disables HTTPS metadata enforcement in Development. #360
- The "forgot password" page no longer reveals whether an email is registered: it now shows a reset link for any submitted address (the link only works for a real account), so the page can't be used to discover which emails have accounts. The on-screen link now also carries a warning that it's a temporary, insecure stand-in that will be removed once email delivery is added. #342
- Production CORS is now locked down: the API only accepts cross-origin requests from explicitly configured origins (`Cors:AllowedOrigins`), and outside Development it refuses to start if no origins are configured instead of silently falling back to allowing any origin. The production origin (`https://financemanager.mikikarkowski.dev`) is now configured. #274
- The API now honours the `X-Forwarded-Proto`/`X-Forwarded-For` headers from the reverse proxy (Cloudflare), so it correctly detects HTTPS and the real client IP in production. This ensures the refresh-token cookie is written with the `Secure` attribute and that per-client rate limiting partitions by the actual client rather than the proxy. #344
- API responses now carry standard hardening headers: HTTP Strict-Transport-Security outside Development, plus `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and a Content-Security-Policy tuned for the Blazor WebAssembly app so the SPA, MudBlazor styles, and SignalR connections still work. #278
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
