# Vendored .NET skills

Vendored from [dotnet/skills — dotnet11](https://github.com/dotnet/skills/tree/96c3d68dad2855e3ab5178aa1a262789d86a1466/plugins/dotnet11), revision `96c3d68dad2855e3ab5178aa1a262789d86a1466` (2026-11-11).

- `process-api-net11`: new System.Diagnostics.Process APIs.
- `system-text-json-net11`: new System.Text.Json APIs.

Codex discovers these repository skills in `.agents/skills/`. Identical copies in `.claude/skills/` support Claude Code without requiring symbolic links on Windows. Each skill includes the upstream MIT license. SKILL.md files are unchanged from upstream.

To update, use the skill-installer helper with `--repo dotnet/skills`, an explicit `--ref`, and both paths under `plugins/dotnet11/skills/`. Replace both repository copies together, retain the license, verify byte equality, and update the revision above.

## Blazor

Source: [dotnet/skills — dotnet-blazor](https://github.com/dotnet/skills/tree/96c3d68dad2855e3ab5178aa1a262789d86a1466/plugins/dotnet-blazor), revision `96c3d68dad2855e3ab5178aa1a262789d86a1466` (2026-11-11).

Installed for this existing Blazor WebAssembly application:

- `author-component`: parameters, lifecycle, async work, disposal, CSS isolation, and code-behind (includes upstream references).
- `collect-user-input`: forms, binding, validation, filters, and uploads.
- `configure-auth`: authorization attributes, conditional UI, and authentication state.
- `coordinate-components`: shared state, cascading values, and change notifications.
- `fetch-and-send-data`: typed API clients, loading states, cancellation, and submissions.
- `plan-ui-change`: component decomposition and data flow for complex pages.
- `use-js-interop`: browser APIs, JS modules, and reference disposal.

Excluded: `create-blazor-project` (scaffolding new projects), `support-prerendering` (no server prerendering in this client), and `use-igniteui-blazor` (the UI uses MudBlazor).

### Repository rules take precedence

The client uses `WebAssemblyHostBuilder` and is served as static files by the ASP.NET Core API. Apply WebAssembly guidance; server/Auto/static-SSR examples are conditional examples, not instructions to change the hosting model. Preserve the custom authentication provider and existing API authentication flow; authentication-state serialization and static SSR Identity pages are not prerequisites here. Components call typed HTTP clients, never a database. Preserve the snapshot refresh rules, MudBlazor conventions, accessible UI behavior, and `[Inject]` property convention in `CLAUDE.md` even where generic upstream examples differ. Planning guidance does not expand the user's authorized scope.

To update these skills, use the same helper and pinned-ref process above with paths under `plugins/dotnet-blazor/skills/`; include all reference files and update both agent copies together.
