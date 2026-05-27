# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

FinanceManager is an online budgeting tool built with Blazor WebAssembly + ASP.NET Core. The Blazor WASM client is hosted and served by the API project as static files. All source lives under `code/`; the repo root also contains published static site artefacts — do not edit those.

The solution file is `code/FinanceManager.slnx`. Target framework is `.NET 10`.

## Commit Messages

Always include the GitHub issue number being resolved in the commit message subject line using `#<number>` (e.g. `Fix bond UI display #174`).

## Branching Workflow

**Feature branches merge into `develop`, never directly into `main`.** When opening a PR for a feature branch, the base must be `develop`. Only `develop` is promoted to `main` (e.g., for releases). If asked to open a PR against `main` from a feature branch, push back and switch the base to `develop`.

### Branch naming

Branch naming is **critical for the changelog to work**. Every changelog entry ends with `#<issue>` (see [`.claude/skills/changelog/SKILL.md`](./.claude/skills/changelog/SKILL.md)), and that number is sourced from the branch → commit → PR chain. If the branch doesn't carry the issue number, the link breaks and the entry can't be written correctly.

**Format**: `<issue-number>-<kebab-issue-title>`

- `<issue-number>` — the GitHub issue number, no `#`, no `issue-` prefix.
- `<kebab-issue-title>` — the issue's title, lower-case, words joined by hyphens, punctuation stripped. Truncate at a word boundary if it would otherwise exceed ~60 characters total.
- No prefixes (`feature/`, `fix/`, `claude/`, your username, etc.) and no suffixes (`-v2`, `-wip`, random tokens).

**Examples**:

| Issue | Branch |
|---|---|
| #128 "Add investment paycheck estimator" | `128-add-investment-paycheck-estimator` |
| #174 "Improve bond UI display" | `174-improve-bond-ui-display` |
| #19 "Date range filter on account transactions" | `19-date-range-filter-on-account-transactions` |

**One issue per branch.** A branch must cover exactly one issue so the resulting commit, PR, and changelog entry all share a single `#<issue>` reference. If the work splits across multiple issues, split the branch.

**No issue? Don't open the branch yet** — create the GitHub issue first so the number exists. Tiny, non-user-visible chores (CI tweaks, dependency bumps, doc typos) that won't get a changelog entry are the only acceptable exception; in that case use a short kebab-case description (`fix-ci-dotnet-test-mtp`) and skip the changelog edit.

## Pull Requests

When opening a pull request that resolves a GitHub issue, the PR body must include a GitHub auto-close keyword referencing the issue (e.g. `closes #123`) so that merging the PR automatically closes the linked issue.

## T-Shirt Size Estimation

Every issue (and the PR that resolves it) carries exactly one `size/*` label so the team — and agents picking up work — know the rough scope before opening the diff. Labels are defined in [`.github/labels.yml`](./.github/labels.yml) and synced to GitHub by the `sync-labels` workflow.

### Sizes

| Label | Effort | What it looks like |
|---|---|---|
| `size/XS` | < 1 hour | Typo, config tweak, one-line fix, doc edit. No new tests. |
| `size/S` | a few hours | One file or one component. Single layer (e.g. just a Razor component, or just one repository method). Light targeted tests. |
| `size/M` | 0.5–2 days | A few files across one or two layers (e.g. controller + service + a repo method). New or significantly updated unit tests. No new external dependency. |
| `size/L` | 2–5 days | Touches most layers of the stack (Domain → Infrastructure → Application → Api → Components), introduces a new abstraction, a new background service, or a new external provider. Needs both unit and integration tests. |
| `size/XL` | > 5 days | Large vertical slice, migration, or architectural change. **Stop and split** into smaller issues before estimating further — XL is a signal, not a target. |

### How agents should estimate

When triaging or creating an issue, pick a size by walking these checks in order — the **highest** matching tier wins:

1. **Surface area.** How many projects under `code/` need to change?
   - 1 project → XS/S
   - 2–3 projects → M
   - 4+ projects, or any change to Domain that ripples through Infrastructure + Application + Api + Components → L
2. **Layer crossings.** Does it cross the layered-monolith boundary (Razor → typed HttpClient → controller → service → repository)?
   - Stays in one layer → XS/S
   - Crosses one boundary → M
   - Crosses two or more boundaries (e.g. new endpoint that needs a new domain entity, EF migration, and a new component) → L
3. **Data layer impact.** Does it need an EF Core migration, a new entity, or a new repository?
   - No → keep current tier
   - Yes → at least M, bump to L if the migration is not trivial (renames, data backfill, new indexes on hot tables).
4. **External dependencies.** Does it add or change a third-party provider (Alpha Vantage, OpenRouter, GitHub Models, Ollama, currency provider)?
   - No → keep current tier
   - Adds a new provider or fallback path → at least L.
5. **Test footprint.** What does "done" require?
   - No new tests → XS
   - One or two unit tests → S
   - New unit tests across multiple files → M
   - New unit **and** integration tests, or new `WebApplicationFactory` overrides → L
6. **Risk / high-churn files.** Does it modify anything from the "High-Churn Files" list below, or `Program.cs`, or any `ServiceCollectionExtension.cs`?
   - Bump one tier up from whatever the previous checks produced (S → M, M → L).
7. **Uncertainty.** Are the requirements vague, or does the change require a design decision before coding?
   - If yes, do **not** estimate yet — ask a clarifying question on the issue first, then size.

If after walking these checks the result is XL, **do not start coding**. Comment on the issue proposing a split (typically: one issue per layer, or one per user-visible feature slice), and re-estimate the children.

### Applying the label

- When creating an issue: include the `size/*` label in the `labels` array.
- When picking up an existing issue with no size label: estimate using the rubric above and add the label as your first action, before opening a branch.
- One size label per issue/PR. If scope changes mid-flight, update the label rather than stacking new ones.

## Changelog

Every code change that has a user-visible effect must add an entry to `CHANGELOG.md` (Keep a Changelog format, CalVer `YY.M.D`). The rules — section headings, wording, issue references, and how to promote `[Unreleased]` — live in [`.claude/skills/changelog/SKILL.md`](./.claude/skills/changelog/SKILL.md). Stage the changelog edit in the same commit as the code change. Pure refactors, test-only changes, CI tweaks, and dependency bumps without behavioural impact do not need a changelog entry.

## Agent Requirements

- **Install the .NET SDK before coding.** Any task that involves code changes requires the .NET 10 SDK to be installed first so that `dotnet build`, `dotnet format`, and `dotnet test` can run. If `dotnet --list-sdks` does not show a `10.*` SDK, install it before making changes — see "Installing the .NET SDK" below for the path that works in the cloud sandbox.
- **Run unit tests after every change.** Every code change must end with `dotnet test --project ./code/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj` passing (see "Running tests" below for the correct invocation). Do not commit, push, or report a task as complete until unit tests are green.

### Installing the .NET SDK (cloud sandbox)

The Microsoft installer hosts (`dot.net`, `aka.ms`, `dotnetcli.azureedge.net`, `builds.dotnet.microsoft.com`) are **not** on the cloud sandbox's network allowlist, so `curl https://dot.net/v1/dotnet-install.sh | bash` fails with `Host not in allowlist`. Use Ubuntu's apt packages instead — Ubuntu 24.04 ships `dotnet-sdk-10.0` directly:

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
dotnet --list-sdks   # should show 10.0.*
```

If the install fails with `404 Not Found` on `.deb` files, the apt index is stale — re-run `sudo apt-get update` and retry. PPA fetch errors (`deadsnakes`, `ondrej/php`) during `apt-get update` are unrelated and can be ignored.

### Running tests

The project's `code/global.json` opts into the new Microsoft.Testing.Platform runner (`xunit.v3.mtp-v2`). Two things together must be right or `dotnet test` falls back to the deprecated VSTest runner and fails:

1. **Run from inside `code/`** (or any subdirectory of it). `global.json` is discovered upward from the current directory; from the repo root it isn't visible, so the SDK uses the legacy runner and rejects `--project` with `MSBUILD : error MSB1001: Unknown switch ... --project`.
2. **Pass `--project`, not a bare path.** The bare `dotnet test ./path/to.csproj` form errors with *"Specifying a project for 'dotnet test' should be via '--project'"*.

```bash
cd code
dotnet test --project ./FinanceManager.UnitTests/FinanceManager.UnitTests.csproj
dotnet test --project ./FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj
```

If you see *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK"*, you're outside `code/` — `cd code` and retry.

## Build and Validation

```bash
# Restore, build, format-check (from repo root is fine)
dotnet restore ./code
dotnet build ./code/FinanceManager.slnx
dotnet format ./code --verify-no-changes --verbosity diagnostic

# Tests must be run from inside code/ (see "Running tests" above)
cd code

# Run all tests
dotnet test --project ./FinanceManager.slnx

# Run only unit tests
dotnet test --project ./FinanceManager.UnitTests/FinanceManager.UnitTests.csproj

# Run only integration tests (requires UseInMemoryDatabase=true env var in CI)
dotnet test --project ./FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj

# Run a single test project with coverage
dotnet test --project ./FinanceManager.UnitTests/FinanceManager.UnitTests.csproj --collect:"XPlat Code Coverage"
```

**Build is strict**: warnings are treated as errors (`Directory.Build.props`). Always run `dotnet build` before committing.

## EF Core Migrations

```bash
dotnet tool install dotnet-ef -g
dotnet ef migrations add <MigrationName> -s code/FinanceManager.Api/FinanceManager.Api.csproj
dotnet ef database update -s code/FinanceManager.Api/FinanceManager.Api.csproj
```

## Architecture

The system is a **layered modular monolith** deployed as a single ASP.NET Core host:

```
Razor component → typed HttpClient → API controller → application/domain service → repository or external provider
```

| Project | Responsibility |
|---------|---------------|
| `FinanceManager` | Blazor WASM bootstrap; registers root component and browser-level services |
| `FinanceManager.Components` | All Razor pages/components, typed HTTP clients, browser-local caches/state — no DB access |
| `FinanceManager.Api` | HTTP routes, JWT auth, CORS, SignalR hub, background services — no domain calculations |
| `FinanceManager.Application` | Use-case orchestration, pricing/insight services, AI/stock provider coordination |
| `FinanceManager.Domain` | Entities, repository interfaces, service contracts, value objects — no infrastructure deps |
| `FinanceManager.Infrastructure` | EF Core `AppDbContext`, repository implementations, external API adapters (Alpha Vantage, currency, AI) |
| `AppHost` | Aspire local orchestration (PostgreSQL + API) |
| `ServiceDefaults` | OpenTelemetry, resilience, health defaults shared across services |

**Key constraint**: `FinanceManager.Domain` must never reference ASP.NET or EF Core. `FinanceManager.Components` must never access the database directly.

### Cross-Cutting Patterns

- **DI registration**: each layer exposes a `ServiceCollectionExtension.cs` with an `Add*` extension method. Wire new services there.
- **Typed HTTP clients** (`code/FinanceManager.Components/HttpClients/`) wrap all API route details; Razor components never call `HttpClient` directly.
- **Provider fallback chain**: AI calls go through a configured fallback (OpenRouter → GitHub Models → Ollama). Stock price reads check repository/cache before hitting Alpha Vantage.
- **Background services + channels**: async jobs (insights, label setting, import) run as hosted services registered in `Program.cs`, communicating via SignalR (`/hubs/currency-import`).
- **Razor code-behind**: complex components split into `.razor` + `.razor.cs` pairs.

### Database

The app supports both **SQL Server** and **PostgreSQL**; the provider is selected at startup in `FinanceManager.Infrastructure/ServiceCollectionExtension.cs`. `AppHost` provisions PostgreSQL locally via Aspire. Development config (`appsettings.Development.json`) may still point at SQL Server — be aware of this drift.

Integration tests use the EF Core **InMemory** provider. They also remove `DatabaseInitializer` and `LabelSetterStartupService` from DI to avoid startup interference.

## C# Conventions

- **Primary constructors** are preferred for classes, records, and structs. For records, use positional syntax: `public record Person(string Name, int Age);`. For classes/structs, map primary constructor parameters to readonly auto-properties — do not add separate private backing fields.
- If a primary constructor cannot be used (complex init, serialization constraints), add a one-line comment explaining why.
- Use **collection expressions** (`[]`, `["a"]`) when the language version allows.
- Latest C# features allowed by the configured `LangVersion` are fair game.
- Private/internal fields: `_camelCase`. Interfaces: `IPascalCase`. Files and types: `PascalCase`.
- Namespaces must match folder paths (`dotnet_style_namespace_match_folder = true` in `.editorconfig`).

## Razor / DI Injection

In any `.razor` component that has an `@code { }` block or a `.razor.cs` code-behind, use `[Inject]` properties — not `@inject` directives in markup.

```razor
@code {
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public ILogger<MyComponent> Logger { get; set; } = default!;
}
```

## Testing Conventions

- Tests live in dedicated projects, not co-located with source.
- Unit tests: use `Moq` for mocking; instantiate controllers/services directly.
- Integration tests: use `WebApplicationFactory` + in-memory EF Core; override DI registrations in `FinanceManagerApiTestApp.cs`; generate JWTs for auth; clear auth headers between tests.
- Test class naming: `*Tests.cs`, grouped by layer (e.g., `Api/Controllers/StockPriceControllerTests.cs`).

## High-Churn Files

These files change frequently — make targeted edits and test carefully around them:

- `code/FinanceManager.Application/ServiceCollectionExtension.cs`
- `code/FinanceManager.Infrastructure/ServiceCollectionExtension.cs`
- `code/FinanceManager.Api/Program.cs`
- `code/FinanceManager.Api/Controllers/StockPriceController.cs`
- `code/FinanceManager.Components/Components/FinancialAccounts/StockAccountComponents/StockAccountDetailsPageContent.razor(.cs)`

## Environment Variables

| Variable | Purpose |
|----------|---------|
| `FINANCE_MANAGER_DB_KEY` | Database connection fallback key |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional OpenTelemetry OTLP export |
| `UseInMemoryDatabase` | Set to `true` to use in-memory DB (integration tests / CI) |

External provider secrets (Alpha Vantage API key, OpenRouter key, GitHub Models token) are configured as empty slots in `appsettings.json` and should be supplied via User Secrets or environment variables — never committed.
