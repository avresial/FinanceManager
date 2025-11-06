Repository AI instructions
--------------------------

C# code style preferences (important):
- Prefer primary constructor syntax for classes/records/structs whenever the project's C# language version supports it.
 - For records prefer positional records: `public record Person(string Name, int Age);`
 - For classes/structs prefer primary constructor parameters mapped to readonly auto-properties instead of introducing separate backing fields. Example:

 ```csharp
 // preferred when allowed by language version
 public class Person(string name, int age)
 {
 public string Name { get; } = name;
 public int Age { get; } = age;
 }
 ```

- Do not add explicit private backing fields solely to support primary-constructor parameters. Prefer readonly auto-properties or primary-record syntax.
- Use the latest C# syntax and features allowed by the project's configured LangVersion/TFM. Respect the project's .editorconfig and project file settings.
- If primary constructors or latest features cannot be used (e.g., complex initialization logic, target language level restriction, serialization constraints), add a one-line comment above the constructor explaining why and fall back to a conventional constructor implementation.
- Do not change public API semantics (method signatures, serialization behavior) without documenting the rationale and adding a unit test suggestion.

Pull request guidance:
- When changes apply primary-constructor conversion, include a short description in the PR listing transformed types and the reason.
- If a change is large, include suggested unit tests or manual verification steps.

Additions:
- Prefer concise code, keep existing naming and .editorconfig conventions.
- For automatic fixes/enforcement, see /tools/roslyn-analyzers (if present).

Razor injection guideline:
- In Razor component files that contain an `@code { }` block or use a code-behind (`.razor.cs`), prefer property injection using the `[Inject]` attribute inside the `@code` block or the code-behind class instead of the `@inject` directive in the markup.
 - This keeps C# code inside code sections and improves clarity and refactorability.
 - Example:

 ```razor
 @using Microsoft.Extensions.Logging

 @code {
 [Inject] public ISnackbar Snackbar { get; set; }
 [Inject] public IAdminUserHttpClient AdminClient { get; set; }
 [Inject] public ILogger<MyComponent> Logger { get; set; }
 }
 ```

- If switching to `[Inject]` is not possible due to project constraints, add a one-line comment explaining the reason.
- When applying this change across many files include a brief PR note listing updated files.



- Prefer collection expressions (`["a"]`, `[]`) when supported.