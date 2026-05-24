# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

FinanceManager is an online budgeting tool built with Blazor WebAssembly + ASP.NET Core. The Blazor WASM client is hosted and served by the API project as static files. All source lives under `code/`; the repo root also contains published static site artefacts — do not edit those.

The solution file is `code/FinanceManager.slnx`. Target framework is `.NET 10`.

## Commit Messages

Always include the GitHub issue number being resolved in the commit message subject line using `#<number>` (e.g. `Fix bond UI display #174`).

## Branching Workflow

**Feature branches merge into `develop`, never directly into `main`.** When opening a PR for a feature branch, the base must be `develop`. Only `develop` is promoted to `main` (e.g., for releases). If asked to open a PR against `main` from a feature branch, push back and switch the base to `develop`.

**Branch naming**: name branches with the issue name only — nothing more. Use the issue's title/identifier as the branch name, with no prefixes, suffixes, or extra descriptors.

## Pull Requests

When opening a pull request that resolves a GitHub issue, the PR body must include a GitHub auto-close keyword referencing the issue (e.g. `closes #123`) so that merging the PR automatically closes the linked issue.

## Build and Validation

```bash
# Restore, build, format-check
dotnet restore ./code
dotnet build ./code/FinanceManager.slnx
dotnet format ./code --verify-no-changes --verbosity diagnostic

# Run all tests
dotnet test ./code/FinanceManager.slnx

# Run only unit tests
dotnet test ./code/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj

# Run only integration tests (requires UseInMemoryDatabase=true env var in CI)
dotnet test ./code/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj

# Run a single test project with coverage
dotnet test ./code/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj --collect:"XPlat Code Coverage"
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
