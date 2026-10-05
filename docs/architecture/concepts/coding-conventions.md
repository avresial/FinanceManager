# Coding conventions

[Architecture index](../README.md) · [8. Crosscutting concepts](../08-crosscutting-concepts.md)

## Naming and organization

Use PascalCase for files, types and methods; interfaces start with `I`, private/internal fields use `_camelCase`, and tests end in `Tests.cs`. Namespaces follow folders. Complex Razor components use `.razor`/`.razor.cs` pairs; one top-level type belongs in each correspondingly named file. Primary constructors and collection expressions follow the repository's C# conventions. Components with code use `[Inject]` properties.

Current examples are [InvestmentValuationController](../../../code/FinanceManager.Api/Features/FinancialAccounts/Investments/Controllers/InvestmentValuationController.cs), [InvestmentPriceProvider](../../../code/FinanceManager.Application/Assets/Pricing/InvestmentPriceProvider.cs), and [LoginServiceTests](../../../code/FinanceManager.Tests.Unit/Components/Features/Identity/Services/LoginServiceTests.cs). Historical examples named StockPriceController/StockPriceProvider were replaced by the investment pricing flow; do not recreate those paths.

## Formatting and dependencies

[.editorconfig](../../../code/.editorconfig) and [Directory.Build.props](../../../code/Directory.Build.props) govern formatting, nullable analysis, code style, and warnings as errors. Import groups are not separated and System usings are not forced first. There is no barrel/export convention or custom import alias scheme; project references express assembly boundaries. Each layer groups DI registration in `ServiceCollectionExtension.cs`; feature modules can have their own registration extensions.

Run from the repository root:

```bash
dotnet build ./code/FinanceManager.slnx
dotnet format ./code/FinanceManager.slnx --verify-no-changes
```

The [CI workflow](../../../.github/workflows/ci.yml) separately checks naming diagnostics (`IDE1006`) and formatting. Repository contribution/branch/changelog rules remain in [CLAUDE.md](../../../CLAUDE.md).

## Errors, logging and sensitive data

Controllers validate boundaries and return appropriate HTTP outcomes. The API's [GlobalExceptionHandler](../../../code/FinanceManager.Api/Shared/Middleware/GlobalExceptionHandler.cs) converts unhandled exceptions to ProblemDetails. Provider adapters have explicit failure/empty-result paths; some browser services still use boolean/default outcomes, so consumers must inspect their actual contracts rather than treat defaults as successful empty financial data.

Use structured `ILogger<T>` messages. [ServiceDefaults](../../../code/ServiceDefaults/Extensions.cs) and [ExternalDependencyLoggingHandler](../../../code/ServiceDefaults/ExternalDependencyLoggingHandler.cs) implement safe dependency diagnostics and suppress raw Polly logs. These specific protections do not prove every log path is redacted. Never log credentials, authorization codes, tokens, maintenance keys, query strings containing secrets, or financial request payloads. General credential rotation and comprehensive logging-policy verification remain open risks in [section 11](../11-risks-and-technical-debt.md).

## Testing conventions

Tests live in dedicated unit, integration, and architecture projects, grouped by feature/layer. Unit tests use Moq and direct construction; integration tests override DI and generate JWTs. No repository-wide minimum coverage percentage is asserted. See [testing](../quality/testing.md) for commands, isolation, relational coverage and limitations.
