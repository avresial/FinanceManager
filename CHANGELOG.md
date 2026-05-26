# Changelog

All notable changes to FinanceManager are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
CalVer (`YY.M.D`) stamped at build time.

See [`.claude/skills/changelog/SKILL.md`](./.claude/skills/changelog/SKILL.md) for the
rules agents must follow when updating this file.

## [Unreleased]

### Changed
- Guest demo data now generates realistic per-account-type entries: currency labels respect income vs expense sign, stock and bond holdings never go negative, and stock prices are prefilled across the seeded range. #206

### Fixed
- Guest dashboard's net cash flow card no longer times out — stock price lookups now preload in bulk per ticker instead of one external resolve per entry. #208

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
