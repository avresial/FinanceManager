# Technology Stack

## Core Sections (Required)

### 1) Runtime Summary

| Area | Value | Evidence |
|------|-------|----------|
| Primary language | C# | `code\Directory.Build.props`, `code\FinanceManager.Api\Program.cs` |
| Runtime + version | .NET 10 (`net10.0`) | `code\Directory.Build.props`, `code\global.json` |
| Package manager | NuGet with central package management via `dotnet` CLI | `code\Directory.Packages.props`, `.github\workflows\ci.yml` |
| Module/build system | Multi-project MSBuild solution centered on `code\FinanceManager.slnx` | `.github\copilot-instructions.md`, `code\FinanceManager.WebUi.csproj`, `code\FinanceManager.Api\FinanceManager.Api.csproj` |

### 2) Production Frameworks and Dependencies

| Dependency | Version | Role in system | Evidence |
|------------|---------|----------------|----------|
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.7 | Blazor WebAssembly client runtime | `code\Directory.Packages.props`, `code\FinanceManager\FinanceManager.WebUi.csproj` |
| Microsoft.AspNetCore.Components.WebAssembly.Server | 10.0.7 | API host serves the built Blazor client | `code\Directory.Packages.props`, `code\FinanceManager.Api\FinanceManager.Api.csproj`, `code\FinanceManager.Api\Program.cs` |
| MudBlazor | 9.4.0 | UI component library | `code\Directory.Packages.props`, `code\FinanceManager\Program.cs`, `code\FinanceManager.Components\FinanceManager.Components.csproj` |
| Blazored.LocalStorage / Blazored.SessionStorage | 4.5.0 / 2.4.0 | Browser session persistence for login/user state | `code\Directory.Packages.props`, `code\FinanceManager\Program.cs`, `code\FinanceManager.Components\Services\LoginService.cs` |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.7 | JWT bearer authentication for API and SignalR hub auth | `code\Directory.Packages.props`, `code\FinanceManager.Api\Program.cs` |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.7 | Relational persistence against SQL Server when configured | `code\Directory.Packages.props`, `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs` |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 | Relational persistence against PostgreSQL when configured | `code\Directory.Packages.props`, `code\FinanceManager.Infrastructure\ServiceCollectionExtension.cs` |
| Aspire.Hosting.AppHost / Aspire.Hosting.PostgreSQL | 13.2.4 | Local distributed orchestration and PostgreSQL provisioning | `code\Directory.Packages.props`, `code\AppHost\AppHost.cs` |
| Microsoft.Extensions.AI.OpenAI / GitHub.Copilot.SDK / OllamaSharp | 10.5.0 / 0.3.0 / 5.4.25 | AI provider integrations with fallback support | `code\Directory.Packages.props`, `code\FinanceManager.Infrastructure\Services\Ai\ServiceCollectionExtension.cs` |
| Blazor-ApexCharts | 6.1.0 | Chart rendering in the client | `code\Directory.Packages.props`, `README.md` |

### 3) Development Toolchain

| Tool | Purpose | Evidence |
|------|---------|----------|
| `dotnet build` | Compile all projects in the solution | `.github\copilot-instructions.md`, `.github\workflows\ci.yml` |
| `dotnet format` | Formatting and code-style verification in CI | `.github\workflows\ci.yml` |
| xUnit v3 + Microsoft.Testing.Platform | Unit and integration test runner | `code\Directory.Packages.props`, `code\global.json`, `code\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj`, `code\FinanceManager.IntegrationTests\FinanceManager.IntegrationTests.csproj` |
| Moq | Mocking in unit tests | `code\Directory.Packages.props`, `code\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj` |
| coverlet | Coverage collection | `code\Directory.Packages.props`, `code\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj`, `code\FinanceManager.IntegrationTests\FinanceManager.IntegrationTests.csproj` |
| GitHub Actions + CodeQL | CI/CD, package vulnerability checks, static analysis, deployment | `.github\workflows\ci.yml` |
| `.editorconfig` + warnings-as-errors | Style enforcement during build | `code\.editorconfig`, `code\Directory.Build.props` |

### 4) Key Commands

```bash
dotnet restore .\code
dotnet build .\code\FinanceManager.slnx
dotnet test .\code\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj
dotnet test .\code\FinanceManager.IntegrationTests\FinanceManager.IntegrationTests.csproj
dotnet format .\code --verify-no-changes --verbosity diagnostic
```

### 5) Environment and Config

- Config sources: `code\FinanceManager.Api\appsettings.json`, `code\FinanceManager.Api\appsettings.Development.json`, `code\FinanceManager.Api\appsettings.test.json`, `code\AppHost\appsettings.json`, `code\AppHost\appsettings.Development.json`, `code\Directory.Build.props`
- Required env vars: `FINANCE_MANAGER_DB_KEY` (database fallback), `OTEL_EXPORTER_OTLP_ENDPOINT` (optional OTLP export toggle), `[TODO] provider-specific secrets for Stock API / OpenRouter / GitHub Models if those integrations are enabled`
- Deployment/runtime constraints: the API project is the deployable web host, while `AppHost` provisions local PostgreSQL for Aspire-based development; the repo root also contains published static site artefacts that are not the source of truth for code changes

### 6) Evidence

- `code\Directory.Build.props`
- `code\Directory.Packages.props`
- `code\global.json`
- `code\FinanceManager\FinanceManager.WebUi.csproj`
- `code\FinanceManager.Api\FinanceManager.Api.csproj`
- `.github\workflows\ci.yml`
- `.github\copilot-instructions.md`

