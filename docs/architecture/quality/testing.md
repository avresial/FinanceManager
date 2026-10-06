# Testing and verification

[Architecture index](../README.md) · [10. Quality requirements](../10-quality-requirements.md)

## Stack and commands

Tests use xUnit v3 with Microsoft.Testing.Platform, xUnit assertions, Moq, WebApplicationFactory and EF Core providers. Package pins and coverage tooling are in [Directory.Packages.props](../../../code/Directory.Packages.props) and the test project files.

Run from **inside `code/`**, because [global.json](../../../code/global.json) selects Microsoft.Testing.Platform. Use `--project`; a bare project path invokes the wrong interface on this SDK.

```bash
cd code
dotnet test --project ./FinanceManager.slnx
dotnet test --project ./FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj
dotnet test --project ./FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj
dotnet test --project ./FinanceManager.Tests.Architecture/FinanceManager.Tests.Architecture.csproj
```

CI runs integration tests with `UseInMemoryDatabase=true`. For local POSIX shells, prefix the integration command with that assignment; in PowerShell set `$env:UseInMemoryDatabase = 'true'` first. Locale-sensitive checks can use `LANG=en_US.UTF-8` and `LC_ALL=en_US.UTF-8` on POSIX systems. No consolidated current coverage report or enforced minimum percentage is documented; do not interpret installed coverlet packages as a measured coverage result. Runner-specific coverage switches must be verified against the selected Microsoft.Testing.Platform version rather than copying legacy VSTest `--collect` commands.

## Scope and layout

| Scope | Targets and evidence | Limits |
| --- | --- | --- |
| Unit | Controllers, domain calculations, application services, browser services and source contracts in [unit tests](../../../code/FinanceManager.Tests.Unit) | Mocks/direct construction do not prove full hosted behavior |
| API integration | Real middleware, authentication and endpoint composition through [FinanceManagerApiTestApp](../../../code/FinanceManager.Tests.Integration/FinanceManagerApiTestApp.cs) | Most host scenarios use EF InMemory, which does not model relational translation or transactions |
| Relational integration | [SQLite recalculation tests](../../../code/FinanceManager.Tests.Integration/Repositories/SqliteRecalculateValuesTests.cs) and provider-specific tests under [integration tests](../../../code/FinanceManager.Tests.Integration) | SQLite cannot establish PostgreSQL/SQL Server parity; Docker/database prerequisites depend on the individual fixture |
| Architecture | [LayerDependencyTests](../../../code/FinanceManager.Tests.Architecture/LayerDependencyTests.cs) and [ControllerCoverageTests](../../../code/FinanceManager.Tests.Architecture/ControllerCoverageTests.cs) | Assembly constraints and controller-test naming checks do not prove every feature boundary or security property |
| Browser/manual | [UI testing instructions](../../../.claude/skills/ui-testing/SKILL.md), [usage instructions](../../../.claude/skills/finance-manager-usage/SKILL.md), [accessibility audit](../audits/accessibility-audit.md) | No complete automated browser E2E suite or accessibility conformance result is established |
| Performance | [Benchmark harness](benchmarks.md) validates responses before measuring server work | Excludes network/TLS, real production DB behavior and concurrent browser workloads |

Tests are not co-located with production code. Files end in `Tests.cs`; feature/layer folders carry the test responsibility. Shared host/controller setup is in [ControllerTests](../../../code/FinanceManager.Tests.Integration/Shared/ControllerTests.cs), [TestDatabase](../../../code/FinanceManager.Tests.Integration/TestDatabase.cs), and [OptionsProvider](../../../code/FinanceManager.Tests.Integration/OptionsProvider.cs).

## Isolation and failure modes

`FinanceManagerApiTestApp` forces an isolated InMemory host by default. Individual tests can replace registrations for their required providers. It removes DatabaseInitializer, LabelSetterStartupService, LogEntryPersistenceBackgroundService and LogRetentionBackgroundService to avoid startup/database races. Rate limiting and startup backfill are disabled by default; specific tests re-enable them. Host filtering permits localhost. Configuration reload watchers are disabled to prevent Linux inotify exhaustion during repeated host creation. Controller helpers clear authentication headers between scenarios; tests must not share mutable identities accidentally.

A green InMemory scenario is insufficient evidence for SQL translation, precision, index behavior, provider migrations, or explicit transactions. Run the relevant relational fixture when those semantics change. Architecture tests load assemblies using marker types: the [architecture model](../../../code/FinanceManager.Tests.Architecture/FinanceManagerArchitecture.cs) deliberately uses assemblies rather than namespaces because some legacy namespaces do not match their physical project.

## Quality interpretation

[Section 10](../10-quality-requirements.md) maps quality scenarios to existing test evidence and gaps. [Section 11](../11-risks-and-technical-debt.md) tracks unresolved verification debt. Test counts and passing status belong to a specific run and revision; this guide does not claim the current suite passed merely because tests exist.
