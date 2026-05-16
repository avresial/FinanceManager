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
