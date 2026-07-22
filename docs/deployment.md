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

## MCP OAuth production configuration

The MCP endpoint is disabled unless `McpOAuth:Enabled` is `true`. Production and hosted development environments must use their public HTTPS origin consistently; internal App Service or container URLs must not appear in OAuth metadata.

Configure these settings through the platform secret/configuration store (Azure App Settings use `__` in place of `:`):

| Setting | Production requirement |
|---------|------------------------|
| `McpOAuth__Enabled` | `true` to publish `/mcp`, OAuth endpoints, and discovery metadata. |
| `McpOAuth__Issuer` | Public HTTPS origin with trailing slash, for example `https://finance.example.com/`. |
| `McpOAuth__Resource` | Exact public MCP URL, normally the issuer origin plus `/mcp`. |
| `McpOAuth__LoginUrl` | Public HTTPS Blazor login URL. |
| `McpOAuth__Clients__0__ClientId` | Public client identifier advertised to the MCP client. It is not a secret. |
| `McpOAuth__Clients__0__DisplayName` | Non-empty operator-facing name for the client; startup validation requires it. |
| `McpOAuth__Clients__0__RedirectUris__0` | Exact redirect URI registered by ChatGPT, Claude, or another client. Add array entries for every supported URI; matching is strict. |
| `McpOAuth__Clients__0__RequirePkce` | `true` unless that specific client is known not to support PKCE. Do not disable PKCE globally. |
| `McpOAuth__SigningCertificatePath` / `McpOAuth__EncryptionCertificatePath` | Absolute paths to persistent PKCS#12 (`.pfx`) files mounted outside the application content directory. |
| `McpOAuth__SigningCertificatePassword` / `McpOAuth__EncryptionCertificatePassword` | Passwords supplied only through the secret store. |

Use separate MCP client entries when clients require different redirect URIs or PKCE behavior. On startup Finance Manager reconciles configured client redirect URIs, permissions, and PKCE requirements with OpenIddict, removing stale configuration. A removed redirect URI therefore stops working after the next successful startup.

### Reverse proxy and discovery URLs

The TLS-terminating proxy must forward the original scheme and client address. Set `ReverseProxy__KnownProxies` or `ReverseProxy__KnownNetworks` to only the actual proxy IPs/CIDR ranges; the application refuses to start outside Development when neither is configured. Never trust forwarded headers from arbitrary sources.

After deployment, verify all returned URLs use the public HTTPS origin:

- `/.well-known/openid-configuration`
- `/.well-known/oauth-authorization-server`
- `/.well-known/oauth-protected-resource/mcp`
- `/.well-known/mcp.json`
- `/connect/mcp`

Also complete one authorization-code flow through the real proxy and confirm that an unauthenticated `/mcp` request returns `401` with resource-metadata discovery. Do not put access tokens or authorization codes in command lines, logs, screenshots, or support tickets.

### Signing and encryption key lifecycle

Development certificates are used only in the local Development environment. Test uses ephemeral keys. Every other environment fails startup unless persistent signing and encryption certificates are configured; production never writes development certificates to the host certificate store.

Store the `.pfx` files in a managed secret/certificate service or a read-only protected mount. Restrict file and secret access to the application identity, back up the certificates according to the service recovery policy, and monitor their expiry dates.

The current configuration supports one active signing certificate and one active encryption certificate, so rotation is deliberately disruptive rather than seamless. Use this runbook:

1. Generate new signing and encryption certificates and stage their `.pfx` files and passwords without replacing the active files.
2. Schedule a reconnect window. Stop or drain all application instances so two different key sets cannot issue tokens concurrently.
3. From a one-off process with the current deployed configuration and database access, run `dotnet FinanceManager.Api.dll --revoke-mcp-client <client-id>`. The command revokes that client's authorizations and tokens through OpenIddict, prints only counts, and exits. A missing client returns exit code `3`; invalid arguments return `2`. Existing SPA JWT and refresh-token records are separate and are not touched.
4. Atomically change all four certificate path/password settings to the new pair, then restart every instance.
5. Verify discovery, complete a new OAuth connection, refresh it once, and call `who_am_i` before restoring traffic.
6. Retain the old certificates only for the audited recovery period, then destroy them through the secret store. Never commit either certificate or password to the repository.

Because old encrypted refresh tokens cannot be used after the key change, connected MCP clients must authorize again. Normal Finance Manager logout ends the browser/SPA session but does not revoke an independent MCP grant; users disconnect in their AI client, while operators revoke compromised MCP grants in the OpenIddict store.

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
