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

The driver below logs in as guest and screenshots a page on mobile and desktop. Save it as `/tmp/ui-shot.mjs` and adapt the navigation for the page you changed.

```js
import pkg from '/tmp/node_modules/playwright/index.js';
const { chromium } = pkg;
// Update the build number to match `ls /opt/pw-browsers/`
const EXE = '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const BASE = 'http://localhost:5113';

const browser = await chromium.launch({ executablePath: EXE, headless: true, args: ['--no-sandbox'] });

async function run(label, width, height) {
  const ctx = await browser.newContext({ viewport: { width, height }, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  try {
    await page.goto(BASE + '/', { waitUntil: 'networkidle', timeout: 120000 });
    await page.waitForTimeout(4000);                       // Blazor WASM boot
    await page.getByText(/check out demo/i).first().click(); // guest login
    await page.waitForTimeout(9000);                       // guest seed + redirect to dashboard

    // --- navigate to the page under test WITHIN the SPA (see gotchas) ---
    if (width < 700) {                                     // mobile drawer is collapsed
      await page.locator('header button').first().click().catch(() => {});
      await page.waitForTimeout(1000);
    }
    await page.getByRole('link', { name: /cash 1/i }).first().click();
    await page.waitForTimeout(9000);

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

- Trigger: the **"Check out demo"** link/button on the landing and login pages. Equivalent to `POST /api/Login` with `{"userName":"guest","password":"GuestPassword"}`, which seeds a sandboxed dataset and returns a JWT stored under `localStorage["userSession"]`.
- After login you land on the dashboard (`/`). The left nav lists the seeded accounts: **Cash 1**, **Loan 1**, **Stock 1**, **Bond 1**. Other routes: `/Assets`, `/Liabilities`, account details open at `/AccountDetails/{id}`.

## Gotchas (these will bite you)

- **Navigate inside the SPA, not by hard URL.** A `page.goto()` to a deep authenticated route (e.g. `/AccountDetails/1`) reloads the WASM app and bounces to `/login` — the auth state lives in the running app. Reach pages by clicking nav links after the guest login.
- **Mobile drawer is collapsed.** On viewports `< 700px` the nav is a temporary overlay; click the app-bar hamburger (`header button`) before clicking an account link.
- **Charts render blank + a red "An unhandled error has occurred" bar appears.** Chart.js is loaded from a CDN that the sandbox blocks (`Chart is not defined`). This is a **sandbox artifact, not a regression** — ignore it. Filter these from logs: `grep -vE "ERR_CERT|Chart is not defined|WebAssemblyRenderer|Unhandled exception"`.
- **The sandbox renders the LIGHT theme; production is dark.** Don't trust absolute colors from the screenshot. For anything theme-dependent (especially text on the app bar or colored surfaces), use palette variables like `color: var(--mud-palette-text-primary);` so it contrasts in both themes, rather than relying on an inherited color that only happens to work in one.
- **`ignoreHTTPSErrors: true`** is required — the dev host emits a self-signed cert for some resources.
- **Always rebuild before re-screenshotting**; `--no-build` serves the last build's `wwwroot`.
