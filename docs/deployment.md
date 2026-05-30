# Deployment

The CI workflow (`.github/workflows/ci.yml`) auto-deploys to two Azure Web Apps:

| Branch    | Azure Web App           | GitHub Environment | Publish-profile secret              |
|-----------|-------------------------|--------------------|-------------------------------------|
| `main`    | `FinanceManagerApi`     | `production`       | `AZURE_WEBAPP_PUBLISH_PROFILE`      |
| `develop` | `FinanceManagerApi-dev` | `development`      | `AZURE_WEBAPP_PUBLISH_PROFILE_DEV`  |

A merge to `develop` triggers build → unit tests → code-quality → security-scan → integration tests → publish → deploy to the dev app. The same chain runs for `main` and deploys to prod. Pull requests run build/tests only and never deploy.

## One-time setup for the develop environment

1. **Create the Azure Web App** (Free F1 tier):
   - Name: `FinanceManagerApi-dev`
   - Runtime: .NET 10 (same as prod)
   - Same region/resource group as prod is fine.
2. **Download the publish profile** from the Azure portal (Web App → *Get publish profile*).
3. **Add it as a GitHub repository secret** named `AZURE_WEBAPP_PUBLISH_PROFILE_DEV` (paste the full XML).
4. **Create the GitHub environment** `development` (Settings → Environments → New environment). No protection rules needed — the goal is automatic deployment on merge.
5. **Configure dev app settings** in the Azure portal so the dev app does not share state with prod:
   - Point `FINANCE_MANAGER_DB_KEY` (or the equivalent connection string) at a separate dev database.
   - Set any other env-specific settings (API keys, AI provider config) as needed.
   - `ASPNETCORE_ENVIRONMENT` can be left as `Production`; the artifact is the same as prod and `appsettings.Development.json` is dev-machine config, not for this environment.

After step 3 is done, the next merge into `develop` will deploy automatically. Validate at `https://FinanceManagerApi-dev.azurewebsites.net`.

## Health probes

The API exposes three health endpoints (mapped in every environment, including production):

| Endpoint         | Purpose   | Auth          | Body                          | Use it for |
|------------------|-----------|---------------|-------------------------------|------------|
| `/alive`         | Liveness  | Anonymous     | `Healthy` / `Unhealthy` (text)| "Is the process responsive?" Checks nothing external, so a dependency outage never trips it. A failure means *restart the instance*. |
| `/health`        | Readiness | Anonymous     | `Healthy` / `Unhealthy` (text)| "Can this instance serve traffic?" Runs all checks, including database connectivity. A failure means *stop routing traffic here* (don't restart). |
| `/health/detail` | Diagnostics | **JWT required** | Per-check JSON breakdown    | Operator/dashboard view: name, status, description, duration, tags, and error per check. Returns `401` to anonymous callers so internal topology is never exposed publicly. |

Liveness and readiness return only the aggregate status word (no per-check detail), so they leak nothing to anonymous probes. `Healthy`/`Degraded` map to HTTP `200`, `Unhealthy` to `503`.

**Configuring the deploy target (Azure Web App):** set the health-check path under *Monitoring → Health check* to `/health` (readiness). Azure restarts instances that fail it. If you later run behind an orchestrator that distinguishes the two, point the liveness probe at `/alive` and the readiness probe at `/health`.

## Free-tier notes

- F1 free tier sleeps after ~20 min of inactivity; the first request after sleep is slow. Acceptable for a validation environment.
- F1 quota is 60 CPU-min/day per app and 10 free apps per region per subscription — well within range for prod + dev.
- F1 does not support deployment slots, so per-PR preview environments aren't possible without upgrading. That's why we deploy on merge to `develop` instead (per the decision on issue #189).

## Feature-branch preview environments (not implemented)

Researched and intentionally skipped — see issue #189 discussion. Re-evaluate if/when the App Service plan moves to Standard or above (deployment slots) or if frontend previews via Azure Static Web Apps become worthwhile.
