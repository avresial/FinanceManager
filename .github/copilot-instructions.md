# FinanceManager Copilot Instructions

Use these instructions for all repository-scoped Copilot work.

## Repository location and scope
- This repository's solution is at `code/FinanceManager.slnx`.
- Keep changes minimal and targeted. Do not refactor unrelated code.
- Respect existing `.editorconfig`, project files, and naming conventions.
- Avoid conflicting guidance across instruction sources.

## Build and validation
- Validate changes from the repository root with:
	- `dotnet build code/FinanceManager.slnx`
- If tests are available for touched areas, run relevant tests and report what was executed.
- If validation cannot be completed, explicitly state what was not run and why.

## Cloud sandbox environment (.NET SDK install + running tests)

If you are running in a cloud sandbox without a preinstalled .NET SDK, install it via apt — **not** the official installer script. The Microsoft installer hosts (`dot.net`, `aka.ms`, `dotnetcli.azureedge.net`, `builds.dotnet.microsoft.com`) are blocked by the sandbox network allowlist, so `curl https://dot.net/v1/dotnet-install.sh | bash` fails with `Host not in allowlist`. Ubuntu 24.04 ships `dotnet-sdk-10.0` directly in its repos:

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
dotnet --list-sdks   # should show 10.0.*
```

Notes:
- If the install hits `404 Not Found` on `.deb` URLs, the apt index is stale — re-run `sudo apt-get update` and retry.
- PPA fetch errors (`deadsnakes`, `ondrej/php`) during `apt-get update` are unrelated and can be ignored.

The project's `code/global.json` opts into the new Microsoft.Testing.Platform runner (`xunit.v3.mtp-v2`). Two things must both be right or `dotnet test` falls back to the deprecated VSTest runner:

1. Run from inside `code/` (or any subdirectory of it). `global.json` is discovered upward from the current directory; from the repo root it isn't visible.
2. Pass `--project`, not a bare path.

```bash
cd code
dotnet test --project ./FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj
dotnet test --project ./FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj
```

If you see *"Testing with VSTest target is no longer supported"* or *"MSBUILD : error MSB1001: Unknown switch ... --project"*, you're outside `code/`. `cd code` and retry.

## C# style preferences (important)
- Prefer primary constructor syntax for classes, records, and structs when supported by the configured C# language version.
- For records, prefer positional records, for example: `public record Person(string Name, int Age);`
- For classes and structs, prefer primary constructor parameters mapped to readonly auto-properties instead of separate private backing fields.

```csharp
public class Person(string name, int age)
{
		public string Name { get; } = name;
		public int Age { get; } = age;
}
```

- Do not add explicit private backing fields solely to support primary constructor parameters.
- Use the latest C# features allowed by the configured LangVersion and target framework.
- If primary constructors or newer features cannot be used (for example complex initialization logic, serialization constraints, or language-level limits), add a one-line comment above the constructor explaining why.
- Do not change public API semantics (method signatures, serialization behavior) without documenting rationale and suggesting a unit test.
- Prefer collection expressions (`["a"]`, `[]`) when supported.

## Razor dependency injection
- In Razor components that contain an `@code { }` block or use code-behind (`.razor.cs`), prefer `[Inject]` properties in C# code over `@inject` in markup.
- If switching to `[Inject]` is not possible due to project constraints, add a one-line comment explaining why.

Example:

```razor
@using Microsoft.Extensions.Logging

@code {
		[Inject] public ISnackbar Snackbar { get; set; } = default!;
		[Inject] public IAdminUserHttpClient AdminClient { get; set; } = default!;
		[Inject] public ILogger<MyComponent> Logger { get; set; } = default!;
}
```

## Pull request guidance
- If a change includes primary-constructor conversions, list transformed types and the reason in the PR description.
- For larger changes, include suggested unit tests or manual verification steps.
- Keep PR notes concise and implementation-focused.

## Additional note
- For automatic fixes and enforcement, see `/tools/roslyn-analyzers` if present.
