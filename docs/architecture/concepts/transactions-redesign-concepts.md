# Transactions page redesign concepts

[Architecture index](../README.md) · [Owning arc42 section](../08-crosscutting-concepts.md)

**Status: design proposal, nothing implemented** ([#993](https://github.com/avresial/FinanceManager/issues/993)). Three concepts for the `/Transactions` page added in #975, each split into components with stable IDs (A1 to C5) so the owner can pick and combine them. A follow-up implementation issue will be filed from the chosen mix.

Baseline: `develop` at `dd933e70` (2026-10-22), guest demo data. The issue asked for `docs/mockups/`. The mockups live in [`concepts/mockups/`](mockups/) instead, next to the existing transaction automation mockup, because the arc42 migration moved them there ([migration inventory](../migration.md)).

## Current page

Sources: [`TransactionsPage.razor`](../../../code/FinanceManager.Components/Features/Dashboard/Components/Pages/TransactionsPage.razor), [`TransactionLogToolbar.razor`](../../../code/FinanceManager.Components/Features/Dashboard/Components/TransactionLogToolbar.razor), [`TransactionLogRow.razor`](../../../code/FinanceManager.Components/Features/Dashboard/Components/TransactionLogRow.razor), [`transaction-log-card.css`](../../../code/FinanceManager.Components/wwwroot/css/transaction-log-card.css).

The audit in #993 found these problems. The tables below refer to them by number:

| # | Problem |
| --- | --- |
| P1 | No overview: no range totals, no day net; the day header spends its right side on the entry count. |
| P2 | Sign-tinted avatar repeats the amount colour, and every cash row has the same wallet icon. |
| P3 | Low desktop density: rows are about 64px, and the name and amount sit about 1400px apart. |
| P4 | The account line repeats on every row and has no visual weight when it matters. |
| P5 | Uneven toolbar (40px select next to 32px controls, search opens a second row); on mobile it scrolls sideways and clips the Accounts filter. |
| P6 | Active filters are hard to see; there is no Clear filters action and no result count. |
| P7 | Thin identity: no time, label or currency; two "Online course" rows on 21 Oct cannot be told apart. |
| P8 | A bare "Load more" button with no sense of how much is loaded. |

### What the endpoint returns today

`GET api/TransactionLog/Page/{userId}` ([controller](../../../code/FinanceManager.Api/Features/Dashboard/Controllers/TransactionLogController.cs)) returns a [`TransactionLogPage`](../../../code/FinanceManager.Domain/Dashboard/Dtos/TransactionLogPage.cs): the items and a next cursor. It returns no total count and no totals. Each [`TransactionLogEntryDto`](../../../code/FinanceManager.Domain/Dashboard/Dtos/TransactionLogEntryDto.cs) has `AccountId, AccountName, AccountType, EntryId, Date, ValueChange, Description`.

Two facts the tables rely on:

- `Date` is the entry's full `PostingDate`, so **time of day is already available**. The account history row already formats it as `HH:mm`.
- Entries carry labels (`FinancialEntryBase.Labels`), and currency accounts carry a `CurrencyId` and an `AccountLabel` (for example `Loan`), but the DTO projects none of them. `AccountType` only distinguishes `Currency`, `Stock` and `Bond`.

"New data needed" values: **none**; **client** (derivable from the rows already loaded); or **new API** (the endpoint must return more). Every new-API item is also marked with a dashed `new API` pill in the mockups.

## Concept A: Ledger

Dense and table-like, desktop first. Only the amount carries the sign colour.

![Concept A: Ledger, desktop, mobile, mobile filter sheet and states](mockups/transactions-concept-a-ledger.svg)

| ID | Component | What changes | New data needed | Fixes |
| --- | --- | --- | --- | --- |
| A1 | Summary strip | One line under the title: *In +X · Out −Y · Net Z · N transactions* for the range and filters, with the resolved range dates on the right. Mobile wraps it to two lines. | **New API**: range summary (in, out, count) for the same query as the page. Loaded pages cannot total a whole range. | P1 |
| A2 | Inline toolbar | Desktop: an always-visible 360px search field on the left, then range, type chips (filled tint and check mark when selected) and the Accounts select, all 32px on one row. Mobile: search, range and a tonal "Filters (2)" button that opens a bottom sheet with range, types, accounts, Clear all and "Show N transactions". | None for the layout. The count on the sheet button uses the A1 summary. | P5, P6 |
| A3 | Single-line rows (36px) | Columns: description, account (type icon and name), label chips, amount. The page uses `MaxWidth.Medium` (960px), so the amount stays within about 930px of the description. Neutral icon; only the amount is coloured. Hover shows a chevron (the row still links to the account). Mobile: a 44px row with type icon, description · account, and amount; labels are hidden. | **New API**: labels on the DTO. | P2, P3, P4, P7 (labels) |
| A4 | Day divider | A thin 28px sticky row, *Thu 22 Oct · net −10.00*, replaces the per-day card and the entry count. | **Client**: the sum of the day's loaded rows. It is exact once the day is fully loaded; for exact values at page boundaries, use daily buckets from the summary (see B1). | P1 |
| A5 | Infinite scroll | A sentinel after the last row loads the next page automatically. The footer reads "Loading more · showing 25 of N". At the end it reads "All N transactions loaded · range starts …" with "Extend to 6M". A failed page shows the current message and Try again. | **New API**: total count (from the A1 summary). The sentinel needs a small `IntersectionObserver` JS interop module, but no new package. | P8 |

## Concept B: Insight feed

Overview first, mobile first. Keeps today's day cards; colour identifies the account, not the sign.

![Concept B: Insight feed, desktop, mobile, account picker sheet and states](mockups/transactions-concept-b-insight-feed.svg)

| ID | Component | What changes | New data needed | Fixes |
| --- | --- | --- | --- | --- |
| B1 | Range overview card | In, out, net and count for the range, plus a daily net bar chart (green up, red down; very large days clamp so the chart stays readable). Hover or tap shows a tooltip; selecting a bar scrolls to that day, loading pages until it is reached. Mobile uses a compact three-column version. | **New API**: range summary with daily buckets. | P1 |
| B2 | Filter chips row | One chip each for range, type, account and search. An active filter is filled with the primary tint and has an × to remove it; an inactive one is outlined with ▾. Clear all and a result count follow. On mobile the row **wraps** to a second line instead of scrolling. Type and Account chips open pickers (bottom sheet on mobile). | None for the layout. The result count uses the B1 summary. | P5, P6 |
| B3 | Grouped day cards | Today's cards stay; the header's right side shows the day net in the sign colour instead of "5 entries". | **Client**, with the same caveat as A4. | P1 |
| B4 | Account-coloured avatars | The avatar tint and icon come from the account, not the sign. A stable colour comes from a fixed eight-colour list (red and green excluded) indexed by `AccountId`. Icons: wallet for cash, bank for loan, trending-up for investments, document for bonds. The amount keeps the sign colour. | **Client** for colour and the cash/stock/bond icons. The loan icon needs **new API**: `AccountLabel` on the DTO or the account list. | P2, P4 |
| B5 | Collapsed account line | When the filter narrows to one account, the account caption is hidden and rows shrink to 48px single-line rows. | None | P4, P3 (mobile) |

## Concept C: Split view

Master-detail for desktop power users. Filters live in a rail; a click inspects a transaction before it navigates.

![Concept C: Split view, desktop, mobile, detail sheet and states](mockups/transactions-concept-c-split-view.svg)

| ID | Component | What changes | New data needed | Fixes |
| --- | --- | --- | --- | --- |
| C1 | Left filter rail | At 1280px and wider, a persistent 240px rail holds range presets with the resolved dates, account checkboxes with each account's net for the range, account-type switches, and Clear filters with the active count. Below 1280px it becomes a drawer opened by a filter icon button with a count badge. | **New API**: per-account totals (summary grouped by account). | P5, P6 |
| C2 | Compact header toolbar | Search, result count, sort (newest, oldest, largest amount) and a disabled export icon button as a placeholder. Mobile: search plus filter and sort icon buttons, with the count on the line below. | **New API**: count; a sort parameter. Today's cursor is keyed on date, account type and entry ID, so amount sort needs a different cursor. | P5, P6 (count) |
| C3 | Two-line rows with metadata | Line 1: description and amount. Line 2: type icon, account · label chip · time. The selected row gets a primary tint and a 3px bar. When the detail panel is closed, the list has a max width of 880px. | Labels: **new API**. Time: **client** (`Date` already includes the posting time). | P2, P3, P4, P7 |
| C4 | Detail panel | Clicking a row selects it and opens a right-side panel (`MudDrawer` `Anchor.End`, persistent) or, on mobile, a bottom sheet. It shows the amount with currency, date and time, an account link, labels, contractor and balance after, plus an "Open in account" button. Keyboard: ↑/↓ move, Enter opens the account, Esc closes. | **New API**: entry details (currency, contractor, balance after, labels), either as a detail endpoint or as extra DTO fields. | P7 |
| C5 | Month headers | A sticky month header with the monthly net and count sits above smaller day subheaders. It is shown when the range spans more than one month (6M, 1Y, long custom ranges). | **New API**: monthly buckets from the summary (loaded rows give only part of a month). | P1, P8 (orientation) |

At 1440px with the navigation drawer open, the desktop width splits into nav 240, rail 240, list 600 and panel 360. Closing either drawer gives the list its 880px.

## Design-pattern cheatsheet

These are UI and interaction patterns: ways to arrange information and support tasks. The comparisons below are expected tradeoffs of the proposals, not results from usability testing. No concept has been selected for implementation.

### A: Ledger — scan and compare entries

| Pattern | Where and why it is used | Tradeoff |
| --- | --- | --- |
| Dense tabular layout and alignment | A3 puts descriptions, accounts and amounts in predictable columns; A4 uses thin day dividers. Repeated positions make scanning many entries and comparing amounts easier. | More rows fit than in B's cards or C's two-line list, but long descriptions and labels have less room. Mobile needs a different row layout and hides labels. |
| Visual hierarchy and proximity | A1 gives totals a compact summary; A3 limits content width and reserves sign colour for amounts. Related values stay closer together with less competing decoration. | More space remains for entries than with B1's chart, but A offers less help spotting trends and less account recognition than B4's avatars. |
| Progressive disclosure | A2 keeps desktop filters visible, then puts secondary mobile controls in a bottom sheet. The filter count signals that hidden choices are active. | Protects mobile list space, but changing an account takes an extra open/close step; B's active chips expose the individual filters more directly. |
| Continuous scrolling with status feedback | A5 loads more entries as the reader reaches the end; loaded/total counts and retry/end states explain progress. | Supports uninterrupted browsing, but returning to an exact position or reaching a footer is harder than with explicit pages. It needs loading announcements and deliberate focus/scroll handling in any future implementation. |

### B: Insight feed — understand the range, then explore

| Pattern | Where and why it is used | Tradeoff |
| --- | --- | --- |
| Overview first, details on demand | B1 starts with totals and daily net bars, then lets the reader select a day and inspect its entries. | Better than A or C for noticing unusual days at a glance; the chart takes space from rows and needs daily aggregates. Jumping to an unloaded day adds loading and navigation complexity. Clamped bars require exact tooltip values to avoid misleading comparisons. |
| Grouping by common region | B3 encloses each day's entries in one card with its net in the header. The shared boundary makes day membership clear. | Easier to read as daily chunks than A's thin dividers, but card padding reduces desktop density. C's month hierarchy gives better orientation across long ranges. |
| Recognition over recall and visible filter state | B4's stable account colours and icons help recognise recurring accounts; B2's filled chips, remove actions and Clear all show and reverse active choices. | Faster account recognition than A's neutral icons after colours become familiar. Eight colours can repeat, so names and icons must carry identity too. Wrapping chips can push entries down on mobile. |
| Remove redundant context | B5 hides account captions when one account is already selected. | Reduces repetition in a single-account view, but the reader must notice the active account filter; C3 keeps that context beside every entry. |

### C: Split view — inspect entries while retaining context

| Pattern | Where and why it is used | Tradeoff |
| --- | --- | --- |
| Master-detail | C3 selects an entry and C4 shows its details beside the list, with keyboard movement between entries. | Better than A or B for inspecting several similar entries without repeatedly navigating away. Selection state, focus and detail loading add complexity; a row click no longer opens the account immediately. |
| Faceted navigation | C1 keeps range, account and type choices together in a persistent rail, with per-account totals; C2 adds search and sort. | Better for repeatedly refining a query than reopening A's sheet or B's pickers, but the rail consumes width and requires grouped totals; amount sorting also changes the API cursor. |
| Progressive disclosure across screen sizes | C4 reveals full metadata only after selection; C1/C4 become a drawer and sheet on smaller screens. | Keeps the list readable without placing every detail in each row. Mobile loses the simultaneous list/detail comparison that makes the desktop concept useful. |
| Hierarchical grouping and secondary metadata | C5 groups months above days; C3 keeps account, label and time below the description. | Better orientation for long ranges and duplicate descriptions than A or B, but adds header/row height and needs monthly aggregates and labels. It is less suited to the quickest compact scan. |

### Which one to choose

| Main task | Prefer | Why it wins; what you give up |
| --- | --- | --- |
| Scan many entries or compare amounts on desktop | **A** | Highest density and direct account navigation. Gives up B's trend overview and C's inspection panel; mobile shows less metadata. |
| Check spending patterns and browse days, especially on mobile | **B** | Strongest overview and account recognition, with familiar day cards. Gives up A's density and C's persistent filters/details; chart navigation costs more client work. |
| Investigate similar entries and repeatedly refine a long-range query | **C** | Keeps details and filters available together on desktop. Requires the most API data and interaction work; at 1440px the open rail and panel leave only 600px for the list. Editing is outside the proposed C4 panel. |

The [recommended mix](#recommended-mix) still fits an incremental improvement: borrow A's compact controls and browsing, C's metadata rows, and B's account cues without adopting the full chart or inspection workflow. Its first pass would omit labels, whole-range counts and the loan-specific icon; day nets derived from loaded rows remain partial at page boundaries. Keep the [mixing constraints](#mixing-guide) when combining patterns, especially C4's change to row-click behaviour.

## State coverage

| State | A: Ledger | B: Insight feed | C: Split view |
| --- | --- | --- | --- |
| Loading | Summary and single-line row skeletons; the toolbar stays usable. | Overview card skeleton with bar placeholders, plus day-card skeletons with avatar squares. | Rail totals, toolbar, month header and two-line row skeletons. |
| Empty | Zero totals, "No transactions match" explaining the filters, Clear filters. | Zero overview with a flat chart, "No transactions for Cash 1", Clear all. | The search is echoed ("No results for “parking”"), rail totals at 0.00, Clear search and Clear filters. |
| Error | The summary shows "—"; "Couldn't load transactions" with Try again in place. | Overview and feed are replaced by one retry; the chips stay so the range can be changed. | Retry in the list; the detail panel closes; filters stay. |
| End of range | "All N transactions loaded · range starts …" and Extend to 6M; a failed next page shows the current message and Try again. | "That's everything from …", a Show 6M chip, and the failed-page message. | "Start of 6M range · N of N loaded", Extend to 1Y, and the failed-page message. |

Mobile toolbar overflow (P5) is fixed in all three: A fits search, range and Filters into 358px and moves the rest into a sheet; B wraps chips onto a second line; C keeps search plus two icon buttons and moves the filters into the drawer.

## Data the endpoint would need

| Addition | Used by | Notes |
| --- | --- | --- |
| Range summary: in, out, net, count; optional daily and monthly buckets; optional per-account totals. Same filters as `Page`. | A1, A2 (count), A5, B1, B2 (count), C1, C2 (count), C5; makes A4/B3 exact | One endpoint serves all three concepts. Mixed currencies must be converted to the user's display currency (as the dashboard services already do) or reported per currency; a plain sum across currencies is wrong. On the client it is a card-like surface, so it should follow the [UI snapshot guide](ui-snapshots.md). |
| Labels on `TransactionLogEntryDto` | A3, C3, C4 | Entries already carry labels; this is a projection change in the service and repositories. |
| `AccountLabel` on the DTO or account list | B4 (loan icon) | Otherwise loans show the cash icon. |
| Currency code | C4 (and any per-row currency display) | From `CurrencyAccount.CurrencyId`; bonds and investments need their own source. |
| Entry details: contractor, balance after | C4 | `CurrencyAccountEntry.ContractorDetails` and `Value`; could be a per-entry detail endpoint. |
| Sort parameter | C2 | Amount sort needs a new cursor shape. |

## Mixing guide

- **Filter surface: pick one** of A2, B2, or C1 with C2. They are alternatives. B2's filled active chip with × and Clear all can style A2's sheet or C1's rail.
- **Overview: A1 or B1.** Both use the same summary endpoint, so A1 can ship first and grow into B1. C5 works with either.
- **Rows: A3, B3 with B4 (and B5), or C3.** A4 dividers pair with A3 or C3; B3 cards pair with B4. B4's avatar can replace C3's type icon.
- **Pagination:** A5 works with every row style.
- **C4** works with any row style but changes what a click does. The dashboard card shares `TransactionLogRow`, so that behaviour must stay page-only.

## Recommended mix

Recommended: **A2 + A4 + C3 + A5, then A1, with B4 and B5 as cheap add-ons.**

- It needs the least backend work. The first pass ships with **no API change**: toolbar and sheet, day net, two-line rows with time, infinite scroll without "of N", and account avatars. That pass fixes P2 to P6, the time part of P7, and most of P8.
- One summary endpoint then unlocks A1 and the counts. Labels on the DTO complete P7.
- One row component (C3) works at both breakpoints. A3 needs a separate mobile row that hides labels, so it is two layouts to keep in sync.
- Click behaviour is unchanged, and so are the shared dashboard row and the existing day grouping.

Alternatives, and when each is the better choice:

- **B-led** (B1 + B2 + B3 + B4 + B5): if mobile is the main way the page is used, or the owner wants the chart. It costs the same summary endpoint plus daily buckets, and the jump-to-day flow is the most complex client behaviour of all the components.
- **C-led** (C1 + C2 + C3 + C4 + C5): if the page becomes a reconciliation tool (checking entries, then editing them in place). It needs the most data (per-account totals, sort, entry details), changes click behaviour, and leaves the list only 600px wide at 1440px while both drawers are open.
