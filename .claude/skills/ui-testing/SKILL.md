---
name: ui-testing
description: Use this skill whenever a change touches the Blazor UI (any .razor/.razor.cs/.razor.css under FinanceManager.Components, layouts, or styling) and you need to see it rendered — not just compiled. It explains how to boot the app locally (macOS) or in the cloud sandbox, log in through the built-in guest/demo account, and screenshot the real pages on both mobile and desktop viewports, in both themes, with the harness browser or Playwright.
---

# Testing the UI on the guest account

A green build and passing unit tests prove the markup compiles — they do **not** prove the page looks right. Any user-visible UI change must be verified by rendering the actual app and looking at a screenshot. The app is auth-gated, so the verified path is the built-in **guest / demo** account, which seeds demo accounts (Cash 1, Loan 1, Investments, Bond 1) in an in-memory database — no credentials, no external DB.

Follow the steps in order. Steps 1-2 start the app; skip them when the app is already running and you were given its address. **Step 3 takes every screenshot with one command — do not write a Playwright script for anything that command can do.**

## 1. Build first

```bash
dotnet build ./code/FinanceManager.slnx
```

The Blazor WASM client is emitted into the API project's `wwwroot` during build, so the server serves stale UI unless you build before booting. (SDK install: see `CLAUDE.md` → "Installing the .NET SDK".)

**Rebuild after `dotnet test`.** Running tests rebuilds the assemblies under a running API, and the client then hangs on the "98%" loading screen. Stop the API, build, and start it again before screenshots.

## 2. Boot the API with the in-memory database

The API project hosts the WASM client as static files. Run it in the background with `UseInMemoryDatabase=true` so it self-seeds and needs no real database. **Pick a free port** — locally, other worktrees may already serve on 5113:

```bash
PORT=5113; while lsof -nP -iTCP:$PORT -sTCP:LISTEN >/dev/null 2>&1; do PORT=$((PORT+1)); done; echo $PORT
cd code
UseInMemoryDatabase=true ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://localhost:$PORT" \
  dotnet run --project ./FinanceManager.Api/FinanceManager.Api.csproj --no-build \
  > /tmp/api-$PORT.log 2>&1   # run in background
```

Wait for it to answer before driving it (do **not** `sleep`-poll for external events — poll the socket):

```bash
curl -s --retry 20 --retry-all-errors --retry-delay 2 \
  -o /dev/null -w "server HTTP %{http_code}\n" http://localhost:$PORT/
```

## 3. Take the screenshots

```bash
dotnet run scripts/Screenshot.cs -- --list          # feature ids: what each one shows and its route
dotnet run scripts/Screenshot.cs -- transactions account-stock account-add-entry cash-flow-forecast alerts \
  --base-url http://localhost:$PORT --out /tmp/shots
```

Run it from the repository root. It signs in as guest once, opens each target inside the running app, waits for the page to settle and prints one PNG path per target, viewport and theme (`/tmp/shots/<target>-<viewport>-<theme>.png`). Defaults: `--viewports mobile,desktop`, `--theme light` (`dark` or `both` for the dark theme). The first run compiles the script, which takes a few seconds; on a new machine run `dotnet run scripts/Screenshot.cs -- --install` once to download Chromium.

**Targets are feature ids** from [`docs/agents/features.json`](../../../docs/agents/features.json): `--list` prints them with a one-line description. Feature ids find accounts by name and open states that need clicks, such as the add-entry dialog (`account-add-entry`, `account-stock-add-trade`), so you never need an account id or a selector. Plain in-app routes (`Transactions`, `""` for the dashboard) work too.

**UI state is addressable by URL**, so a route can open it directly: `AccountDetails/<id>?dialog=add-entry` opens the add-entry form; `Transactions?search=<text>&range=1M` pre-fills the Transactions page filters.

## 4. Look at the screenshots

**Read every PNG the command printed** and actually look at it. A blank frame where content should be is a failure, not a pass. Check the changed component in both viewports and both themes.

If your model cannot read images, do not skip this or judge it from DOM data alone. Have a small vision model inspect the PNGs against a numbered checklist of what changed, and quote its verdict:

```bash
claude -p "<numbered checklist> Images: /tmp/shots/transactions-desktop-light.png /tmp/shots/transactions-mobile-light.png" --model haiku --allowedTools Read
codex exec --skip-git-repo-check -m gpt-6-luna "<numbered checklist>" -i /tmp/shots/transactions-desktop-light.png </dev/null
```

Put the prompt before `--allowedTools`/`-i`: both flags take a list and would swallow it. Back the verdict with DOM measurements where they help (element rects, `scrollHeight > clientHeight` for clipping, computed colours).

## 5. Clean up

Stop only the API you started, by the PID listening on your port. **Never `pkill -f FinanceManager.Api`**: locally that also kills other worktrees' servers.

```bash
lsof -nP -tiTCP:$PORT -sTCP:LISTEN | xargs kill
```

## Only when the command cannot reach a state: a custom driver

Use this for interactions the command cannot do (hover states, typing into forms, a single component). For interactive exploration, a harness browser (for example T3 Code `preview_*`/`device_screenshot`, or the Claude browser pane) also works: open `http://localhost:$PORT/DevelopLogin/guest/<page>`. Say which one you used.

### Playwright setup

**Local macOS.** Playwright 1.64.x uses the cached `chromium-1248`, so install only the npm package and do not set `executablePath`:

```bash
mkdir -p /tmp/pw-scratch && cd /tmp/pw-scratch && npm init -y >/dev/null 2>&1
PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1 npm i playwright@1.64
ls ~/Library/Caches/ms-playwright/    # must list chromium-1248; otherwise match the version to a cached build
```

**Cloud sandbox.** **Do not run `npx playwright install` — the Playwright CDN is blocked** (`Host not in allowlist`). Install the npm package the same way and point `executablePath` at the pre-installed build: `ls /opt/pw-browsers/`, then `/opt/pw-browsers/chromium-<build>/chrome-linux/chrome`.

### Driver

The driver signs in through the **auto test login** entry path (`/DevelopLogin/guest/{page}`, see
[`finance-manager-usage`](../finance-manager-usage/SKILL.md)) — one `goto` boots the WASM app, logs in as
guest, and lands directly on the page under test. Save it as `/tmp/pw-scratch/ui-shot.mjs`:

```js
import { chromium } from 'playwright';
const BASE = `http://localhost:${process.env.PORT ?? 5113}`;
const TARGET = process.env.TARGET ?? '';   // in-app route, e.g. 'AccountDetails/1'; '' for the dashboard
// Cloud sandbox only: set PW_CHROMIUM to /opt/pw-browsers/chromium-<build>/chrome-linux/chrome.
const browser = await chromium.launch({ headless: true, executablePath: process.env.PW_CHROMIUM, args: ['--no-sandbox'] });

async function run(label, width, height, colorScheme) {
  // The app picks its theme from the system setting, so colorScheme switches light/dark.
  const ctx = await browser.newContext({ viewport: { width, height }, colorScheme, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  try {
    await page.goto(`${BASE}/DevelopLogin/guest/${TARGET}`, { waitUntil: 'networkidle', timeout: 120000 });
    // The app sets <html data-fm-ready="true"> once the routed page has no API request in flight.
    await page.waitForSelector('html[data-fm-ready=true]', { state: 'attached', timeout: 60000 });
    console.log(label, 'url:', page.url());
    await page.screenshot({ path: `/tmp/ui-${label}.png` });
  } finally {
    await ctx.close();
  }
}

for (const scheme of ['light', 'dark']) {
  await run(`mobile-${scheme}`, 430, 920, scheme);
  await run(`desktop-${scheme}`, 1280, 1000, scheme);
}
await browser.close();
```

```bash
cd /tmp/pw-scratch && PORT=$PORT TARGET='AccountDetails/1' node ui-shot.mjs
```

**Screenshot one component** with `page.locator('<selector>').screenshot({ path })`. It scrolls the element into view, including inside inner scroll containers: dashboard cards sit in a scrollable grid that `window.scrollTo` does not move, so a `clip` computed from `getBoundingClientRect()` after `window.scrollTo` comes out empty.

**Wait for `html[data-fm-ready=true]`, never a fixed sleep.** The app removes the mark on every navigation and sets it again once the page's API requests have settled. After an in-SPA navigation of your own, wait for the mark to come back; after a click that does not navigate, wait for the element you expect instead.

**Hover states** need a real pointer: `await locator.hover()` or `page.mouse.move(x, y)`. Dispatching synthetic `mouseenter` events does not trigger Blazor's handlers reliably.

## Guest login facts (for reference)

- Preferred trigger: **`/DevelopLogin/guest`** (optionally `/DevelopLogin/guest/{page}` to deep-link), the develop-only auto test login documented in `AGENTS.md`. It is disabled (404) in Production/Release.
- Manual fallback: the **"Check out demo"** link/button on the landing and login pages. Equivalent to `POST /api/Login` with `{"userName":"guest","password":"GuestPassword"}`. Either way the JWT is stored under `localStorage["userSession"]`.
- After login you land on the dashboard (`/`) unless you deep-linked. The left nav lists the seeded accounts: **Cash 1**, **Loan 1**, **Investments** (the stock/ETF account), **Bond 1**. Other routes: `/Assets`, `/Liabilities`, account details open at `/AccountDetails/{id}`.

## Gotchas (these will bite you)

- **Deep-link through `/DevelopLogin/guest/{page}`, not by hard URL.** A `page.goto()` straight to an authenticated route (e.g. `/AccountDetails/1`) reloads the WASM app and bounces to `/login` — the auth state lives in the running app. Going to `/DevelopLogin/guest/AccountDetails/1` instead authenticates first and then navigates within the SPA. In-SPA clicks after login also work.
- **Mobile drawer is collapsed.** On viewports `< 700px` the nav is a temporary overlay; if you need to click nav links, open the app-bar hamburger (`header button`) first.
- **Cloud sandbox only: charts render blank + a red "An unhandled error has occurred" bar appears.** The chart script CDN is blocked there (`Chart is not defined`). This is a **sandbox artifact, not a regression** — ignore it. Filter these from logs: `grep -vE "ERR_CERT|Chart is not defined|WebAssemblyRenderer|Unhandled exception"`. Locally, charts must render; a blank chart is a real failure.
- **Playwright renders the light theme unless you set `colorScheme: 'dark'`.** Capture both. Use palette variables like `color: var(--mud-palette-text-primary);` so text contrasts in both themes, rather than relying on an inherited color that only happens to work in one.
- **Simulate latency for loading states.** The local in-memory API answers too fast to show refresh indicators; add `page.route('**/api/**', r => setTimeout(() => r.continue(), 2500))` to capture in-flight states.
- **`ignoreHTTPSErrors: true`** is required — the dev host emits a self-signed cert for some resources.
- **Always rebuild before re-screenshotting**; `--no-build` serves the last build's `wwwroot`.
