---
name: ui-testing
description: Use this skill whenever a change touches the Blazor UI (any .razor/.razor.cs/.razor.css under FinanceManager.Components, layouts, or styling) and you need to see it rendered — not just compiled. It explains how to boot the app in the cloud sandbox, log in through the built-in guest/demo account, and screenshot the real pages on both mobile and desktop viewports using the pre-installed Playwright Chromium.
---

# Testing the UI on the guest account

A green build and passing unit tests prove the markup compiles — they do **not** prove the page looks right. Any user-visible UI change must be verified by rendering the actual app and looking at a screenshot. The app is auth-gated, so the verified path is the built-in **guest / demo** account, which seeds a full set of demo accounts (Cash, Stock, Bond, Loan) in an in-memory database — no credentials, no external DB.

This is the exact workflow; follow it verbatim.

## 1. Build first

```bash
dotnet build ./code/FinanceManager.slnx
```

The Blazor WASM client is emitted into the API project's `wwwroot` during build, so the server serves stale UI unless you build before booting. (SDK install: see `CLAUDE.md` → "Installing the .NET SDK".)

## 2. Boot the API with the in-memory database

The API project hosts the WASM client as static files. Run it in the background with `UseInMemoryDatabase=true` so it self-seeds and needs no real database. Pick a free port.

```bash
cd code
UseInMemoryDatabase=true ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://localhost:5113" \
  dotnet run --project ./FinanceManager.Api/FinanceManager.Api.csproj --no-build \
  > /tmp/api-run.log 2>&1   # run in background
```

Wait for it to answer before driving it (do **not** `sleep`-poll for external events — poll the socket):

```bash
curl -s --retry 20 --retry-all-errors --retry-delay 2 \
  -o /dev/null -w "server HTTP %{http_code}\n" http://localhost:5113/
```

## 3. Drive a browser with the pre-installed Chromium

**Do not run `npx playwright install` — the Playwright CDN is blocked** in the sandbox (`Host not in allowlist`). A Chromium build is already on disk at `/opt/pw-browsers/`; point Playwright's `executablePath` at it. You only need the `playwright` npm package (from npm, which *is* allowed):

```bash
cd /tmp && npm init -y >/dev/null 2>&1 && npm i playwright
ls /opt/pw-browsers/    # find the chromium-<build> dir, e.g. chromium-1194
```

The driver below signs in through the **auto test login** entry path (`/DevelopLogin/guest/{page}`, see
`AGENTS.md`) — one `goto` boots the WASM app, logs in as guest, and lands directly on the page under test.
Save it as `/tmp/ui-shot.mjs` and set `TARGET` to the route you changed (empty string = dashboard).

```js
import pkg from '/tmp/node_modules/playwright/index.js';
const { chromium } = pkg;
// Update the build number to match `ls /opt/pw-browsers/`
const EXE = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const BASE = 'http://localhost:5113';
const TARGET = 'AccountDetails/1';   // in-app route to screenshot; '' for the dashboard

const browser = await chromium.launch({ executablePath: EXE, headless: true, args: ['--no-sandbox'] });

async function run(label, width, height) {
  const ctx = await browser.newContext({ viewport: { width, height }, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  try {
    await page.goto(`${BASE}/DevelopLogin/guest/${TARGET}`, { waitUntil: 'networkidle', timeout: 120000 });
    await page.waitForTimeout(12000);                      // WASM boot + guest seed + redirect to TARGET

    console.log(label, 'url:', page.url());
    await page.screenshot({ path: `/tmp/ui-${label}.png` });
  } finally {
    await ctx.close();
  }
}

await run('mobile', 430, 920);
await run('desktop', 1280, 1000);
await browser.close();
```

```bash
cd /tmp && node ui-shot.mjs
```

Then **Read `/tmp/ui-*.png`** and actually look at it. A blank frame where content should be is a failure, not a pass.

## 4. Clean up

```bash
pkill -f "FinanceManager.Api"
```

## Guest login facts (for reference)

- Preferred trigger: **`/DevelopLogin/guest`** (optionally `/DevelopLogin/guest/{page}` to deep-link), the develop-only auto test login documented in `AGENTS.md`. It is disabled (404) in Production/Release.
- Manual fallback: the **"Check out demo"** link/button on the landing and login pages. Equivalent to `POST /api/Login` with `{"userName":"guest","password":"GuestPassword"}`. Either way the JWT is stored under `localStorage["userSession"]`.
- After login you land on the dashboard (`/`) unless you deep-linked. The left nav lists the seeded accounts: **Cash 1**, **Loan 1**, **Stock 1**, **Bond 1**. Other routes: `/Assets`, `/Liabilities`, account details open at `/AccountDetails/{id}`.

## Gotchas (these will bite you)

- **Deep-link through `/DevelopLogin/guest/{page}`, not by hard URL.** A `page.goto()` straight to an authenticated route (e.g. `/AccountDetails/1`) reloads the WASM app and bounces to `/login` — the auth state lives in the running app. Going to `/DevelopLogin/guest/AccountDetails/1` instead authenticates first and then navigates within the SPA. In-SPA clicks after login also work.
- **Mobile drawer is collapsed.** On viewports `< 700px` the nav is a temporary overlay; if you need to click nav links, open the app-bar hamburger (`header button`) first.
- **Charts render blank + a red "An unhandled error has occurred" bar appears.** Chart.js is loaded from a CDN that the sandbox blocks (`Chart is not defined`). This is a **sandbox artifact, not a regression** — ignore it. Filter these from logs: `grep -vE "ERR_CERT|Chart is not defined|WebAssemblyRenderer|Unhandled exception"`.
- **The sandbox renders the LIGHT theme; production is dark.** Don't trust absolute colors from the screenshot. For anything theme-dependent (especially text on the app bar or colored surfaces), use palette variables like `color: var(--mud-palette-text-primary);` so it contrasts in both themes, rather than relying on an inherited color that only happens to work in one.
- **`ignoreHTTPSErrors: true`** is required — the dev host emits a self-signed cert for some resources.
- **Always rebuild before re-screenshotting**; `--no-build` serves the last build's `wwwroot`.
