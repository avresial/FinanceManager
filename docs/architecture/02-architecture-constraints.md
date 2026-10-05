# 2. Architecture constraints

[Architecture index](README.md)

## 2.1 Technical constraints

| Constraint | Consequence | Evidence |
|---|---|---|
| .NET 10 and C# with `LangVersion=latest`, nullable reference types, implicit usings, style enforcement, and warnings as errors | New projects inherit shared settings; build warnings are failures | [Directory.Build.props](../../code/Directory.Build.props) |
| One MSBuild solution with centrally managed NuGet versions | Add dependencies in the owning `.csproj` and set their version centrally; avoid copied version tables as the maintenance source | [FinanceManager.slnx](../../code/FinanceManager.slnx), [Directory.Packages.props](../../code/Directory.Packages.props) |
| Blazor WebAssembly hosted by ASP.NET Core | Browser components access data through HTTP clients; the API publishes the browser assets | [WASM bootstrap](../../code/FinanceManager/Program.cs), [API bootstrap](../../code/FinanceManager.Api/Program.cs) |
| Domain has no project/package references to ASP.NET Core or EF Core | Keep persistence and web transport implementations outside Domain | [Domain project](../../code/FinanceManager.Domain/FinanceManager.Domain.csproj), [layer dependency tests](../../code/FinanceManager.Tests.Architecture/LayerDependencyTests.cs) |
| Components must not depend on API/Infrastructure or access EF Core | Use feature HTTP clients and browser-local state; direct database access is prohibited | [Components project](../../code/FinanceManager.Components/FinanceManager.Components.csproj), [layer dependency tests](../../code/FinanceManager.Tests.Architecture/LayerDependencyTests.cs) |
| SQL Server and PostgreSQL/Supabase relational paths coexist | Provider inference, migration compatibility, and environment configuration require explicit checks | [database registration](../../code/FinanceManager.Infrastructure/ServiceCollectionExtension.cs), [migration guide](operations/migrations.md) |
| Some services are shared with the browser | Application is not completely transport-neutral: it references Components authorization abstractions and registers browser authentication/settings services | [Application project](../../code/FinanceManager.Application/FinanceManager.Application.csproj), [Application registrations](../../code/FinanceManager.Application/ServiceCollectionExtension.cs) |

The intended boundaries are constraints for new work, not a claim that every existing class follows ideal Clean Architecture. The [actual dependency graph](05-building-block-view.md#52-project-reference-view) and [solution strategy](04-solution-strategy.md) explain the implemented compromises.

### Technology baseline

The following versions were verified in the central package file during this migration. They document the baseline, not a promise to remain pinned to these versions. The [central package file](../../code/Directory.Packages.props) is authoritative after subsequent updates.

| Dependency | Baseline | Role |
|---|---|---|
| ASP.NET Core WebAssembly, WebAssembly.Server, JwtBearer | 10.0.10 | Browser runtime, static hosting, bearer authentication |
| MudBlazor | 9.8.0 | Browser UI components |
| Blazor-ApexCharts | 7.0.0 | Chart rendering |
| Blazored.LocalStorage / SessionStorage | 4.5.0 / 2.4.0 | Browser persisted state |
| EF Core SQL Server / InMemory / SQLite | 10.0.10 | Relational production path and test database providers |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL persistence |
| Aspire.Hosting.AppHost / PostgreSQL | 13.4.6 | Local orchestration and PostgreSQL container provisioning |
| Microsoft.Extensions.AI.OpenAI | 10.8.3 | OpenAI-compatible AI client support |
| GitHub.Copilot.SDK / OllamaSharp | 1.0.0-beta.4 / 5.4.25 | Copilot and Ollama adapters |
| ModelContextProtocol.AspNetCore / OpenIddict.AspNetCore | 2.1.0 / 7.6.0 | MCP HTTP transport and OAuth |
| Microsoft.Extensions.Caching.Hybrid | 10.8.0 | Server cache decorators and invalidation |
| xunit.v3.mtp-v2 / Moq / bunit | 3.2.2 / 4.20.72 / 2.9.0 | Tests, mocks, and component verification |
| TngTech.ArchUnitNET.xUnitV3 / BenchmarkDotNet | 0.13.3 / 0.15.8 | Architecture rules and benchmarks |
| coverlet.collector / coverlet.msbuild | 10.0.1 / 10.0.1 | Coverage tooling |

## 2.2 Development and organizational constraints

[CLAUDE.md](../../CLAUDE.md) and [AGENTS.md](../../AGENTS.md) govern repository work. Feature branches are issue-scoped and merge into `develop`; `main` is the release branch. The source of truth is `code/`. Generated build/publish output must not be edited as source. Earlier documentation referred to published root assets and `sample-data/`; neither is present in this checkout. Repository data currently includes [resources](../../resources) and [README images](../../ReadmeElements/Imgs).

Use PascalCase filenames/types, `_camelCase` private fields, `IPascalCase` interfaces, and namespaces matching folders. Feature folders refine project-level separation. Complex Razor components use `.razor` plus `.razor.cs`; DI registrations are grouped behind `Add*` extensions. Full conventions are maintained in [coding conventions](concepts/coding-conventions.md) and [`.editorconfig`](../../code/.editorconfig).

The test runner is Microsoft.Testing.Platform, selected by [global.json](../../code/global.json). Run tests from `code/` so the runner configuration is discovered, and pass `--project`. CI also invokes the built test executables. The shared props target .NET 10; `global.json` selects the test runner and does not pin an SDK version.

```bash
# From repository root
dotnet restore ./code/FinanceManager.slnx
dotnet build ./code/FinanceManager.slnx
dotnet format ./code --verify-no-changes --verbosity diagnostic

# Tests: run from code/; --project is required by this runner
cd code
dotnet test --project ./FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj
dotnet test --project ./FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj
dotnet test --project ./FinanceManager.Tests.Architecture/FinanceManager.Tests.Architecture.csproj --configuration Debug
```

The Debug architecture build avoids optimization affecting IL-based dependency analysis. GitHub Actions builds, checks formatting/naming, scans vulnerable packages and CodeQL, runs unit/integration/architecture tests, then publishes the API only after the required jobs succeed. See [CI configuration](../../.github/workflows/ci.yml) and [testing](quality/testing.md) for coverage collection and provider-specific test prerequisites.

## 2.3 Configuration and runtime constraints

Committed API configuration includes [Development](../../code/FinanceManager.Api/appsettings.Development.json), [Production](../../code/FinanceManager.Api/appsettings.Production.json), and [test](../../code/FinanceManager.Api/appsettings.test.json) settings, supplemented by environment variables and secrets. The earlier stack document listed an API `appsettings.json`; no such tracked file exists in this checkout. The browser has its own [wwwroot/appsettings.json](../../code/FinanceManager/wwwroot/appsettings.json). Aspire has separate [AppHost settings](../../code/AppHost/appsettings.json) and [Development overlay](../../code/AppHost/appsettings.Development.json).

| Configuration input | Meaning and precedence |
|---|---|
| `ConnectionStrings:FinanceManagerDb` | First relational connection choice, normally injected by Aspire |
| `ConnectionStrings:DefaultConnection` | Second connection choice; the committed Development overlay contains a machine-specific SQL Server connection |
| `FINANCE_MANAGER_DB_KEY` | Last connection fallback, not a universally required variable |
| `DatabaseProvider` | Fallback provider choice; recognizable connection-string keys can override it through inference |
| `UseInMemoryDatabase` | Select EF InMemory; test hosts set this explicitly |
| `EnableGuestSessionSandbox` | Enable per-guest InMemory contexts alongside relational contexts; this prevents ordinary context pooling |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional export destination; see [service defaults](../../code/ServiceDefaults/Extensions.cs) |
| `JwtConfig`, `Cors:AllowedOrigins`, `ReverseProxy:KnownProxies/KnownNetworks` | Startup-validated security configuration, with stricter requirements outside Development |
| `McpOAuth:Enabled` and related issuer/resource/client/certificate settings | Gate OAuth/MCP availability; Production overlay currently disables it |
| External-service and AI-provider options | Keys, URLs, model choices, enabled state, priorities, and fallback entries; see [integrations](concepts/integrations.md) |

Connection selection/inference is implemented by [AddDatabase](../../code/FinanceManager.Infrastructure/ServiceCollectionExtension.cs). Do not assume the Development SQL Server setting wins over Aspire's PostgreSQL connection. Secret values should be supplied through user secrets, deployment secrets, or configuration providers rather than copied into documentation.

Outside Development, CORS origins and trusted reverse proxies are mandatory, JWT configuration is validated, and HTTPS-related middleware applies. MCP requires production certificates when enabled. Password reset routes are intentionally disabled outside Development pending transactional email delivery. Verify these constraints against [API security registration](../../code/FinanceManager.Api/ApiConfigurationExtensions.cs), [PasswordResetController](../../code/FinanceManager.Api/Features/Identity/Controllers/PasswordResetController.cs), and the [deployment guide](operations/deployment.md).
