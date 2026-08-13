# Accessibility audit: keyboard navigation and semantics

Audit date: 13 August 2026

Baseline: WCAG 2.2 Level A and AA

Repository baseline: `4a8ef7cd83f3256205b38682e565c9bce5abdd89`

## 1. Scope and methodology

This is a research-only audit. It changes no application behavior. The review covered the authenticated dashboard, Assets, Liabilities, Cash account details, Add account, and User settings, plus the shared layout, navigation, forms, overlays, tables, charts, and custom dashboard controls.

The evidence came from three complementary methods:

1. A source review of Razor, component code, and CSS in `FinanceManager.Components`.
2. Keyboard operation in headless Chromium at 1280 by 1000 pixels, with a 430 by 920 mobile navigation pass. The application ran in Development with an in-memory guest account through `/DevelopLogin/guest/{page}`.
3. DOM and accessibility inspection with axe-core 4.11.1 using the WCAG 2 A/AA, 2.1 AA, and 2.2 AA rule sets. The audit sampled Tab order and computed focus styles and exercised Enter, Escape, and arrow-key behavior on representative menus, selectors, and the custom date picker.

The automated sample reported issues, not a compliance score. Each finding below was checked against rendered output and source before inclusion. Automated checks cannot prove accessibility or replace assistive-technology testing.

### Routes and interactions exercised

| Route | Sampled keyboard or semantic behavior |
| --- | --- |
| `/` | Full Tab pass, Customize menu open with Enter and close with Escape |
| `/Assets` | Full Tab pass, chart/filter/card controls, slider, scroll regions |
| `/Liabilities` | Full Tab pass, charts and filter controls |
| `/AccountDetails/1` | First 55 Tab stops, ranges, toolbar, transactions, custom date control |
| `/AddAccount` | Full Tab pass, account type selector with Enter, Arrow Down, and Escape |
| `/UserSettings` | Full Tab pass, section selector, forms, subscription cards, status content |
| Mobile `/Assets` | First 20 Tab stops with the temporary drawer closed |

No VoiceOver, NVDA, JAWS, or other speech session was run. The screen-reader section is therefore a source/accessibility-tree assessment and a test plan, not a claim of verified speech output. The development environment successfully rendered ApexCharts; CDN-dependent Chart.js behavior remains an environment limitation where applicable.

Standards references: [WCAG 2.2](https://www.w3.org/TR/WCAG22/), [WAI-ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/), and [Using ARIA](https://www.w3.org/TR/using-aria/). Native HTML should be preferred to reconstructing controls with ARIA.

## 2. Current accessibility assessment

FinanceManager has a useful foundation: primary and account navigation are named; many icon-only application controls have labels; custom transaction rows accept Enter and Space; loading state has a named live status; the diversification gauge has a text alternative; standard MudBlazor inputs generally expose labels; and the sampled Customize menu restored focus when Escape closed it.

The application is not yet ready to claim WCAG 2.2 AA conformance. The highest-impact gaps are keyboard access to application-owned click targets, the closed mobile drawer remaining in sequential focus order, missing control names, light-theme contrast, and financial visualizations without equivalent structured data. Page structure, status announcements, overlays, and focus presentation need a systematic pass rather than isolated ARIA additions.

The sample also found positive third-party behavior that should be protected with tests:

- the Add account combobox opened with Enter, retained logical focus while Arrow Down changed the active option, and closed with Escape;
- the Customize menu moved focus into its items and returned focus to the trigger on Escape;
- primary routes and account links followed a consistent desktop order;
- native buttons used for presets and carousel arrows remained keyboard reachable.

## 3. Keyboard and navigation findings

### K1 — Closed mobile drawer remains in the Tab order (high)

With the temporary drawer closed at 430 pixels, pressing Tab after the menu toggle focused the off-screen Dashboard link at `x = -240`, followed by every other drawer action. A keyboard user must traverse invisible content before reaching the page. This is a focus-order problem under WCAG 2.4.3 and can make the interface appear stuck.

The responsive state only changes `DrawerVariant` and `Open` in `Shared/Layout/MainLayout.razor:95`; the links remain rendered in `Shared/Layout/MainLayout.razor:17` and `Shared/Layout/NavMenu.razor:2`.

Recommendation: when the temporary drawer is closed, remove its descendants from sequential navigation and the accessibility tree using behavior supported by MudBlazor or an `inert`/hidden implementation. When opened, move focus into it, support Escape, and return focus to the toggle. Add desktop and mobile keyboard regression tests.

### K2 — Subscription plan cards are pointer-only (high)

The plan choices in `Features/Identity/Components/UserSettingsPage.razor:177` use `MudPaper` with only `@onclick` at line 184. They are absent from the sampled Tab sequence and expose neither selection semantics nor keyboard activation. This fails the intent of WCAG 2.1.1 for an application-owned function.

Recommendation: model the choices as native radios inside a named group, or as native buttons with a separately conveyed selected state. Avoid adding `role="button"` to `MudPaper` unless native controls cannot meet the layout requirement.

### K3 — Logout uses link styling for an action (medium)

`Shared/Layout/MainLayout.razor:29` uses `MudNavLink` with an `@onclick` action and no destination. It is focusable in the rendered sample, but its DOM was a `div` without a button role. Enter/Space behavior and announcement therefore depend on undocumented component behavior.

Recommendation: use a native button-styled navigation item and test both Enter and Space. This is more robust than repairing the role and keyboard contract manually.

### K4 — Repeated navigation has no bypass mechanism (medium)

`Shared/Layout/MainLayout.razor:11` places the app bar and drawer before `MudMainContent` at line 33. No skip link is present. The sampled desktop routes required traversing the common navigation before page controls.

Recommendation: add a visible-on-focus “Skip to main content” link and a stable focus target at the main content. On client-side route changes, update the document title and move focus to the new page heading when that does not disrupt an in-progress action.

### K5 — Focus indication is inconsistent and needs visual verification (medium)

Computed styles commonly reported `outline-style: none`; some controls indicated focus using border/text color, while charts used the browser outline and the investment preset buttons had a custom two-pixel outline. The active amber navigation state can be mistaken for focus. Because computed outline alone cannot determine the complete visual result, this is a confirmed inconsistency rather than a blanket claim that every focus indicator is absent.

Recommendation: define a theme-aware `:focus-visible` treatment with sufficient contrast, area, and separation under WCAG 2.4.7 and 2.4.11. Visually test it in light/dark themes and at desktop/mobile sizes.

### K6 — Overlay focus behavior is not defined consistently (medium)

Several financial-account edit/add/remove experiences use `MudOverlay` plus `MudPaper`, for example `BondAccountDetailsPageContent.razor:87` and `BondTransactionRow.razor:120`, rather than a dialog primitive with an explicit accessible name and focus contract. The source does not define initial focus, containment, Escape handling, or trigger-focus restoration. The custom date picker did not expose a dialog in the automated attempt, so its focus behavior remains a required manual validation rather than a confirmed failure.

Recommendation: migrate modal interactions to one shared dialog pattern and test open, initial focus, forward/reverse containment, Escape/cancel, validation, and focus restoration.

## 4. ARIA and semantic HTML findings

### S1 — Main landmark and page heading hierarchy are incomplete (high)

The rendered sample contained header, aside, and two named navigation landmarks but no `<main>` landmark. `MudMainContent` at `Shared/Layout/MainLayout.razor:33` did not render one. None of the six routes contained an `h1`; content began at levels 3 to 6. Examples include “Account settings” as h4 at `UserSettingsPage.razor:27` and chart/card headings as h5/h6.

An h1 is not independently required by WCAG, but a programmatic page title and logical hierarchy are needed for efficient navigation and understanding under WCAG 1.3.1 and 2.4.6.

Recommendation: render exactly one clearly named main landmark and make each route’s visible title the h1. Nest card/section headings without skipped levels. Do not promote financial values or currency symbols to headings solely for typography.

### S2 — Application-owned controls lack accessible names (high)

Axe reproduced three critical name failures:

- the investment paycheck slider on `/Assets` had no accessible name; its source is `InvestmentPaycheckEstimatorCard.razor:75`;
- one Assets icon button was unnamed;
- the account-history split-menu icon button was unnamed on `/AccountDetails/1`; its source is `Shared/AccountHistoryToolbar.razor:58`;
- the password-field end icon button on `/UserSettings` was unnamed.

Recommendation: give each control a concise purpose-based accessible name, associate visible slider text with the range input, and make show/hide-password labels reflect current state. Add component or browser assertions that every interactive element has a non-empty accessible name.

### S3 — Custom filter/list structures have missing or ambiguous names (medium)

The dashboard produced an unnamed `role="listbox"`; User settings produced an unnamed list structure. Filter controls visually show “Type”, “Account”, or “Wallet”, but the relationship between the group, current selection, and individual choices is not always exposed consistently. Some rendered filter buttons used `role="checkbox"`, which is useful only if checked state and group purpose are both available.

Recommendation: name each group, expose current checked/selected state, and use a native fieldset/radio or checkbox group where its interaction matches the design. Validate MudBlazor output rather than adding redundant roles to its internal DOM.

### S4 — Progress and decorative graphics are exposed without names (medium)

The dashboard reported eight unnamed progress bars from `FinancialLabelsListCardView.razor:114`. The adjacent visible percentage does not programmatically name each bar. Avatar/icon wrappers in `FinancialLabelsListCardView.razor:105`, `TransactionLogCard.razor:53`, account details, and settings were rendered with image roles but lacked alternatives, producing repeated `role-img-alt` failures.

Recommendation: either hide decorative icons/avatars from assistive technology or give informative graphics a contextual name. Name progress bars from their category and percentage, or mark them decorative when the adjacent text conveys the same data.

### S5 — Scrollable card regions are not keyboard operable (medium)

Axe flagged the Money by label and Transaction log `overflow-y-auto` containers (`FinancialLabelsListCardView.razor:21`, `TransactionLogCard.razor:13`) and the diversification card’s scrollable main area as scroll regions without a focusable region or focusable descendants.

Recommendation: prefer content that expands with the page. If an inner scroll region is necessary, give it a name, make it keyboard focusable, ensure a visible focus indicator, and verify arrow/Page Up/Page Down behavior.

### S6 — Light-theme amber text fails contrast in sampled contexts (high)

Axe repeatedly measured `#ffb300` text on white at approximately 1.79:1, below WCAG 1.4.3 for normal text. It affected navigation/headings and selected states across all sampled routes. The light palette sets primary to `Colors.Amber.Darken1` in `FinanceManager/App.razor:51` while the app bar is white at line 55.

Recommendation: choose separate accessible foreground and surface/accent tokens. Test normal text, large text, focus indicators, disabled states, error/success amounts, and selected navigation in both themes. Do not rely on dark-theme success to cover the light theme.

### S7 — Chart semantics do not provide an equivalent data view (high)

ApexCharts exposed sampled charts as focusable SVG `role="application"` objects with names such as “area chart with 1 data series”. That identifies the widget but does not communicate dates, values, trends, comparison series, or units. `Shared/Components/Charts/TimeSeriesValueCard.razor:69` and `FinancialAccounts/Components/Shared/AccountDetailsHero.razor:100` render visual series without an adjacent structured summary or data table.

Recommendation: provide a concise text summary and an expandable accessible data table for every decision-relevant chart. Include chart title, period, units/currency, current or total value, direction/change, and each series. Treat keyboard interaction with the SVG as supplemental, not the only path to the information.

### S8 — Dynamic status and validation need an announcement strategy (medium)

The sticky “You have unsaved changes” content at `UserSettingsPage.razor:252` appears dynamically without an explicit status region. Settings messages at line 232 and dashboard error/empty states likewise rely on component defaults that are not protected by an application-level contract. `Shared/Components/Loading/DisplaySpinner.razor:1` is a positive example with `role="status"`, `aria-live`, and `aria-busy`.

Recommendation: define when a message should use `role="status"`, `aria-live="polite"`, or `role="alert"`; associate field errors with their fields; avoid re-announcing whole regions during refresh; and test loading-to-success, validation, save, delete, and background-refresh transitions.

## 5. Screen-reader and text-to-speech considerations

The structure/name issues above are likely to affect screen-reader users, but their exact speech, browse-mode navigation, and interaction behavior must be verified with real assistive technology.

A dedicated manual pass should cover:

1. VoiceOver with Safari on macOS and NVDA with Firefox or Chrome on Windows.
2. Landmark and heading navigation on every primary route.
3. Form label, helper, error, required, invalid, and disabled announcements.
4. Menu, combobox, date picker, drawer, and modal entry/exit behavior.
5. Transaction-row expanded/collapsed state. The custom rows already implement Enter and Space in their `OnKeyDown` handlers, but `aria-expanded` and the relationship to revealed content should be verified.
6. Chart summaries and equivalent data after those are implemented.
7. Status announcements for loading, refresh failure, save, import, delete, and unsaved changes.
8. Reading order and duplicate/redundant announcements from MudBlazor icons, avatars, labels, and nested interactive structures.

## 6. AI-agent and machine-readable UI considerations

These recommendations are optional machine-operability improvements, not accessibility violations. AI agents benefit first from the same native elements, names, landmarks, labels, states, and structured data that humans using assistive technology need. Accessibility semantics must remain the primary contract.

- Keep stable `data-testid` values for high-value workflows after semantics are correct: dashboard cards, date ranges, account rows, transaction actions, settings sections, and confirmation messages.
- Expose localized financial display text together with structured values and ISO currency/date metadata where practical. Do not replace human-readable text with test-only attributes.
- Give charts an accessible table; it is also a much safer machine-readable interface than scraping SVG geometry or tooltips.
- Represent loading, stale, error, empty, selected, expanded, and unsaved states programmatically. Prefer standard ARIA/native states; add test metadata only when no standard state expresses the application concept.
- Avoid using CSS class names, DOM position, generated Blazor attributes, or icon glyphs as automation contracts.
- For external automation, prefer the application API or MCP surface over UI scraping when it exposes the same authorized operation and data.

## 7. Prioritized findings

| ID | Priority | Finding | Primary references | WCAG relevance |
| --- | --- | --- | --- | --- |
| K1 | P0 | Closed mobile drawer links remain off-screen in Tab order | `MainLayout.razor:17`, `NavMenu.razor:2` | 2.4.3 |
| K2 | P0 | Subscription plans are pointer-only | `UserSettingsPage.razor:177` | 2.1.1, 4.1.2 |
| S2 | P0 | Slider, menu, and password icon controls lack names | `InvestmentPaycheckEstimatorCard.razor:75`, `AccountHistoryToolbar.razor:58`, `UserSettingsPage.razor:127` | 1.3.1, 4.1.2 |
| S6 | P1 | Light-theme primary text has insufficient contrast | `FinanceManager/App.razor:51` | 1.4.3 |
| S7 | P1 | Financial charts lack equivalent structured content | `TimeSeriesValueCard.razor:69`, `AccountDetailsHero.razor:100` | 1.1.1, 1.3.1 |
| S1 | P1 | No main landmark and weak route heading hierarchy | `MainLayout.razor:33`, route components | 1.3.1, 2.4.6 |
| K6 | P1 | Overlay/modal focus contract is inconsistent | financial-account `MudOverlay` usages | 2.1.2, 2.4.3 |
| K3 | P2 | Logout is not a native button | `MainLayout.razor:29` | 2.1.1, 4.1.2 |
| K4 | P2 | No skip-to-content path | `MainLayout.razor:11` | 2.4.1 |
| K5 | P2 | Focus presentation is inconsistent | global/component CSS | 2.4.7, 2.4.11 |
| S3 | P2 | Filter/list group names and states are inconsistent | dashboard filters, `UserSettingsPage.razor:13` | 1.3.1, 4.1.2 |
| S4 | P2 | Progress bars and decorative image roles are unnamed | `FinancialLabelsListCardView.razor:105` | 1.1.1, 4.1.2 |
| S5 | P2 | Nested scroll regions are not keyboard operable | dashboard card content | 2.1.1 |
| S8 | P2 | Dynamic status/validation announcement contract is absent | `UserSettingsPage.razor:232` | 3.3.1, 4.1.3 |

## 8. Concrete recommendations

1. Fix the two keyboard blockers first: closed-drawer focus and subscription plan selection.
2. Add accessible-name tests for all rendered controls and resolve the slider, split-menu, and password-toggle failures.
3. Establish shared layout semantics: skip link, named main landmark, one route h1, route-change focus policy, and a consistent focus-visible token.
4. Create one accessible modal/dialog pattern and replace application-owned overlay dialogs incrementally.
5. Audit the light/dark palette with automated contrast checks plus visual review.
6. Add a reusable chart summary/data-table pattern and require it for new financial charts.
7. Normalize decorative versus informative icon/avatar/progress semantics.
8. Define status and validation announcement rules and use the existing named loading status as the reference pattern.
9. Add automated axe and keyboard smoke tests for representative routes, but keep a manual assistive-technology checklist for release testing.
10. Add stable machine metadata only after the native accessibility contract is correct.

## 9. Staged implementation plan

### Stage 1 — Remove blockers and prevent regressions

- Fix K1, K2, and S2.
- Add keyboard tests for closed/open mobile navigation, plan selection, named controls, Customize Escape restoration, and the account-type selector.
- Add axe smoke tests for the six audited routes in both themes.

### Stage 2 — Establish application-wide structure and focus

- Implement K3, K4, K5, S1, and K6 through shared layout/dialog primitives.
- Define route heading, focus restoration, focus-visible, and status-message conventions in the component documentation.
- Validate desktop and mobile behavior with VoiceOver and NVDA.

### Stage 3 — Make financial information equivalent without vision

- Implement S4, S5, S7, and S8.
- Add chart summaries and downloadable/expandable tables with currency, units, periods, and series names.
- Audit transaction lists/tables, expandable rows, filters, import/export, and destructive confirmation flows end to end.

### Stage 4 — Improve machine operability

- Add stable test identifiers and structured financial metadata where standard semantics are insufficient.
- Prefer API/MCP contracts for data-oriented automation.
- Document selectors and state contracts, and add tests that prevent accidental removal.

### Completion criteria for the remediation program

- All P0/P1 findings have focused regression tests and are manually verified.
- Representative routes have no reproducible WCAG A/AA axe violations after false-positive review.
- Keyboard-only users can reach, operate, and exit every interactive surface with visible focus and no off-screen stops.
- VoiceOver and NVDA passes cover landmarks, headings, forms, dialogs, live states, tables, and chart alternatives.
- Remaining exceptions are documented with owner, rationale, compensating behavior, and follow-up date.
