---
name: finance-manager-usage
description: How to use the running FinanceManager app as an agent — above all the develop-only auto test login (/DevelopLogin/{login}/{page}) that skips the landing page and login form. Use this skill whenever you need to sign in to the app for testing (UI or API), deep-link to an authenticated page, or obtain an access token without credentials. Covers guest vs testuser accounts, environment gating, and direct API usage.
---

# Using FinanceManager as an agent

## Auto test login — skip the landing page and login form

**Never walk the landing page → login form → "Check out demo" click sequence when testing.** The app
has a develop-only auto-login entry path that signs you in with a single navigation:

```
{webAppAddress}:{port}/DevelopLogin/{login}
{webAppAddress}:{port}/DevelopLogin/{login}/{page}
```

Opening the URL logs you in immediately and redirects to the dashboard, or to `{page}` when one is given.

### `{login}` — who you sign in as

| Login | Backing data | Use when |
|---|---|---|
| `guest` | Fresh in-memory sandbox seeded with demo accounts (Cash, Loan, Stock, Bond) — no real database touched | Cloud/sandbox testing, or any time you just need a populated UI (`UseInMemoryDatabase=true` works fine) |
| `testuser` | The seeded `testuser` account on the real develop database | Local testing with Aspire and a real database running |

`testuser` requires the account to have been seeded, which happens automatically at startup when
`Seeding:TestUserPassword` is configured (it is, in `appsettings.Development.json`). If it isn't seeded the
endpoint answers `503` with an explanation — fall back to `guest`.

### `{page}` — optional deep link

Any in-app route, including ones with segments. Omit it to land on the dashboard (`/`).

```
http://localhost:5113/DevelopLogin/guest                    → dashboard
http://localhost:5113/DevelopLogin/guest/Assets             → assets page
http://localhost:5113/DevelopLogin/guest/AccountDetails/1   → account details for account 1
http://localhost:5113/DevelopLogin/testuser/UserSettings    → settings, as testuser
```

Because the login happens inside the running WASM app, this also solves the "deep links bounce to /login"
problem — a hard `page.goto()` to a `/DevelopLogin/...` URL authenticates first and then navigates within
the SPA, so the target page renders authenticated.

### Where it works

The UI page is backed by `POST api/DevelopLogin/{login}`, which answers **404 whenever
`ASPNETCORE_ENVIRONMENT` is `Production` or `Release`** — the feature effectively does not exist outside
development-like environments. Both logins are passwordless; no credentials appear in test scripts.

### Calling the API directly (no browser)

When you only need an access token, skip the UI entirely:

```bash
curl -s -X POST http://localhost:5113/api/DevelopLogin/guest
# → {"userName":"guest","userRole":"User","userId":...,"accessToken":"...","expiresIn":...}
```

Use the returned `accessToken` as an `Authorization: Bearer` header. `testuser` additionally sets the
refresh-token cookie, like a regular login.

## Rendering and screenshotting the UI

For booting the app in the cloud sandbox and driving it with the pre-installed Chromium (blocked CDNs,
theme gotchas, viewport requirements), follow
[`.claude/skills/ui-testing/SKILL.md`](../ui-testing/SKILL.md) — it uses the auto test login above as its
entry point.
