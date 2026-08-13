# Progressive Web App (PWA) Caching and Offline Behavior

This document describes FinanceManager's Progressive Web App (PWA) caching strategy, offline scope, and update lifecycle.

---

## 1. Executive Summary & Root Cause Audit

FinanceManager delivers a Blazor WebAssembly frontend served by an ASP.NET Core backend host. Due to the multi-megabyte footprint of WebAssembly runtimes and compiled .NET assemblies (`.dll`/`.wasm`), repeat visits require instant asset availability to avoid user-perceived load latency.

### Audit Findings & Root Cause Analysis
1. **Service Worker implementation (#554)**:
   - Commit `8ed7db88` introduced dual-environment service workers (`service-worker.js` for development and `service-worker.published.js` for production) registered via `FinanceManager.WebUi.csproj` and `index.html`.
2. **Missing installation metadata (#690)**:
   - In baseline commit `f2552096fe28d890b99282431ea0429cfdf2cab9`, `index.html` lacked the `<link rel="manifest" href="manifest.json">` declaration, and `wwwroot/manifest.json` was missing.
   - **Root cause**: The worker provided offline caching, but the browser had no web app manifest to describe an installable app. The current implementation links `manifest.webmanifest` and provides the required identity, display, color, and icon metadata.

---

## 2. Environment Behaviors: Production vs. Development

| Feature / Behavior | Development (`service-worker.js`) | Production (`service-worker.published.js`) |
| :--- | :--- | :--- |
| **Primary Goal** | Frictionless DX & instant code refresh | Instant repeat boots & offline availability |
| **Caching Mechanism** | None (Network Only) | Pre-caches boot assets from `service-worker-assets.js` |
| **Fetch Event Listener** | Passthrough empty handler: `self.addEventListener('fetch', () => {})` | Custom `onFetch` interceptor for offline fallback |
| **Asset Manifest** | Excluded | Imports `service-worker-assets.js` generated during publish build |
| **Update Impact** | Always fetches latest code from dev server | Silent background install; applies on next full tab close |

---

## 3. Caching Strategy & Asset Filtering

Production service worker caching uses an **offline asset filter** strategy to balance offline capability with minimal storage footprint.

### Precache Inclusion Rules (`offlineAssetsInclude`)
The service worker precaches essential boot assets required for the WASM application shell:
- **Application Binaries**: `.dll`, `.wasm`
- **Markup & Configuration**: `.html`, `.json`
- **Logic & Styling**: `.js`, `.css`
- **Fonts & Icons**: `.woff2`, `.woff`, `.ico`, and the two declared PWA icons
- **Installation Metadata**: `manifest.webmanifest`
- **Blazor Framework Data**: `.dat`, `.blat`

### Precache Exclusion Rules (`offlineAssetsExclude`)
The following are explicitly excluded from offline precaching:
- **Emitted Worker Script**: `service-worker.js` is excluded to prevent a worker self-caching loop.
- **Heavy Media / Images**: `.jpg`, `.jpeg`, general `.png`, and `.mp4` files are excluded. Large images rely on standard HTTP browser caching rather than taking up offline storage quota.

---

## 4. Offline Scope & Navigation Fallback

FinanceManager is a Single Page Application (SPA). Client-side routing is handled entirely in WebAssembly after initial shell boot.

### Navigation Interception (`shouldServeIndexHtml`)
When the browser initiates a navigation request (`event.request.mode === 'navigate'`):
1. If the URL corresponds to an offline static asset registered in `manifestUrlList`, that asset is served directly.
2. Otherwise, for general SPA route navigations (for example `/Assets`), the service worker responds with the cached shell `index.html`.
3. Blazor's client-side router then inspects the URL path and renders the requested view offline.

---

## 5. API & Hub Network-Only Bypass

To maintain data integrity, security, and live real-time communication, specific routes **must never** be served from offline static caches:

- **REST API Routes (`/api/`)**: Requests containing `/api/` bypass index.html fallback and go directly to the network. This guarantees authentication tokens, transaction mutations, and budget queries are always fresh.
- **SignalR Hub Connections (`/hubs/`)**: Real-time channels (e.g. currency import progress hubs) bypass service worker interception to preserve WebSockets/HTTP long-polling handshakes.

---

## 6. Update Lifecycle & Cache Clean-up

To avoid breaking active user sessions with mid-session script mismatches, FinanceManager uses a **conservative update strategy**:

1. **Background Installation**:
   - When a new deployment occurs, the browser detects changes in `service-worker.published.js` or `service-worker-assets.js` and installs the new worker in the background.
   - The worker refrains from calling `skipWaiting()` or `clients.claim()`.
2. **Quiet Activation**:
   - The new service worker waits until **all open tabs** running the older version are closed.
   - Upon opening a new tab, the new service worker activates silently.
3. **Stale Cache Purge (`onActivate`)**:
   - During `activate`, the service worker queries all browser cache keys (`caches.keys()`).
   - Any cache key starting with `offline-cache-` that does not match the current `cacheName` (`offline-cache-${self.assetsManifest.version}`) is immediately deleted (`caches.delete(key)`).

The registration uses `updateViaCache: 'none'` and explicitly checks for an update on page load. The host serves the worker script, generated asset manifest, and web app manifest with `Cache-Control: no-cache`, so intermediary caches cannot indefinitely hide a new deployment.

---

## 7. Verification & Automated Test Coverage

Static source contracts for PWA behavior are continuously validated via focused unit tests in:
`code/FinanceManager.Tests.Unit/Pwa/PwaSourceContractTests.cs`

Run the test suite using:
```bash
cd code
dotnet test ./FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj -- --filter-class "*PwaSourceContractTests"
```

For a production verification, publish the API host, load it in a fresh browser profile, and confirm:

1. the application shell and declared manifest icons are present in the versioned cache;
2. a client route loads after the browser is taken offline;
3. API and SignalR routes are not replaced with the cached application shell; and
4. after the generated asset-manifest version changes and every old tab closes, the new worker activates and removes the old versioned cache.
