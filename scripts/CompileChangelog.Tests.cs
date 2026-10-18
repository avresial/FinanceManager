// Black-box tests for CompileChangelog.cs: each case runs the script against a temporary repository root.
// Usage (from the repository root): dotnet run scripts/CompileChangelog.Tests.cs
using System.Diagnostics;
using System.Runtime.CompilerServices;

const string Header = "# Changelog\n\nIntro.\n";
const string PreviousBlock = "## [26.5.25] - 2026-05-25\n\n### Added\n- Old entry. #1\n";
var failures = new List<string>();

Test("check accepts valid fragments and changes nothing", () =>
{
    using var repo = new TempRepo(Header + "\n" + PreviousBlock);
    repo.Fragment("12-a.md", "---\ntype: Fixed\n---\nFix a thing. #12\n");
    repo.Fragment("no-issue.md", "---\r\ntype: \"Added\"\r\n---\r\n\r\n- Add a thing.\r\n");
    var result = repo.Run("--check");
    Expect(result.ExitCode == 0, $"exit code {result.ExitCode}: {result.Output}");
    Expect(result.Output.Contains("2 changelog fragment(s) valid."), result.Output);
    Expect(repo.Changelog == Header + "\n" + PreviousBlock, "CHANGELOG.md changed");
    Expect(repo.FragmentNames.Count == 3, "fragments were deleted"); // two fragments plus .gitkeep
});

Test("check reports every invalid fragment with an actionable message", () =>
{
    using var repo = new TempRepo(Header + "\n" + PreviousBlock);
    repo.Fragment("1-bad-type.md", "---\ntype: Feature\n---\nAdd it. #1\n");
    repo.Fragment("2-no-front-matter.md", "Add it. #2\n");
    repo.Fragment("3-two-lines.md", "---\ntype: Added\n---\nAdd it. #3\nAnd more. #3\n");
    repo.Fragment("4-html.md", "---\ntype: Added\n---\nAdd <b>bold</b> text. #4\n");
    repo.Fragment("5-mismatch.md", "---\ntype: Added\n---\nAdd it. #50\n");
    repo.Fragment("6-unknown-key.md", "---\ntype: Added\nscope: ui\n---\nAdd it. #6\n");
    repo.Fragment("Bad_Name.md", "---\ntype: Added\n---\nAdd it.\n");
    repo.Fragment("orphan-issue.md", "---\ntype: Added\n---\nAdd it. #7\n");
    repo.Fragment("8-code-span.md", "---\ntype: Added\n---\nAccept `List<int>` values. #8\n");
    var result = repo.Run("--check");
    Expect(result.ExitCode == 1, $"exit code {result.ExitCode}");
    foreach (var expected in new[]
    {
        "1-bad-type.md: Type 'Feature' is not allowed",
        "2-no-front-matter.md: Fragment must start with YAML front matter",
        "3-two-lines.md: Fragment body must be exactly one non-empty entry line after the front matter; found 2",
        "4-html.md: Entry contains HTML",
        "5-mismatch.md: Entry must end with '#5'",
        "6-unknown-key.md: Unknown front matter key 'scope'",
        "Bad_Name.md: File name must be lower-case kebab-case",
        "orphan-issue.md: Entry references an issue but the file name does not",
        "8 changelog error(s).",
    })
        Expect(result.Output.Contains(expected), $"missing '{expected}' in:\n{result.Output}");
    Expect(!result.Output.Contains("8-code-span.md"), "HTML inside a code span was rejected");
});

Test("compile creates today's block in section order and deletes fragments", () =>
{
    using var repo = new TempRepo(Header + "\n" + PreviousBlock);
    repo.Fragment("9-fix.md", "---\ntype: Fixed\n---\nFix nine. #9\n");
    repo.Fragment("10-add.md", "---\ntype: Added\n---\nAdd ten. #10\n");
    repo.Fragment("100-add.md", "---\ntype: Added\n---\nAdd hundred. #100\n");
    repo.Fragment("tidy-up.md", "---\ntype: Security\n---\nHarden headers.\n");
    var result = repo.Run();
    Expect(result.ExitCode == 0, $"exit code {result.ExitCode}: {result.Output}");
    Expect(repo.Changelog == Header + "\n## [26.10.18] - 2026-10-18\n\n### Added\n- Add hundred. #100\n- Add ten. #10\n\n"
        + "### Fixed\n- Fix nine. #9\n\n### Security\n- Harden headers.\n\n" + PreviousBlock, repo.Changelog);
    Expect(repo.FragmentNames.SequenceEqual([".gitkeep"]), string.Join(", ", repo.FragmentNames));
});

Test("compile merges into an existing block for today without duplicates", () =>
{
    var today = "## [26.10.18] - 2026-10-18\n\n### Changed\n- Change one. #2\n\n### Added\n- Add one. #3\n\n";
    using var repo = new TempRepo(Header + "\n" + today + PreviousBlock);
    repo.Fragment("3-add-one.md", "---\ntype: Added\n---\nAdd one. #3\n");
    repo.Fragment("4-add-two.md", "---\ntype: Added\n---\nAdd two. #4\n");
    repo.Fragment("5-remove.md", "---\ntype: Removed\n---\nRemove five. #5\n");
    var result = repo.Run();
    Expect(result.ExitCode == 0, $"exit code {result.ExitCode}: {result.Output}");
    Expect(repo.Changelog == Header + "\n## [26.10.18] - 2026-10-18\n\n### Added\n- Add two. #4\n- Add one. #3\n\n"
        + "### Changed\n- Change one. #2\n\n### Removed\n- Remove five. #5\n\n" + PreviousBlock, repo.Changelog);
});

Test("compile keeps every entry when today's block repeats sections", () =>
{
    // The migrated block for 26.10.18 repeats section headings; merging folds them into one of each.
    var today = "## [26.10.18] - 2026-10-18\n\n### Added\n- A. #1\n\n### Fixed\n- F. #2\n\n### Added\n- B. #3\n\n";
    using var repo = new TempRepo(Header + "\n" + today + PreviousBlock);
    repo.Fragment("4-c.md", "---\ntype: Added\n---\nC. #4\n");
    var result = repo.Run();
    Expect(result.ExitCode == 0, $"exit code {result.ExitCode}: {result.Output}");
    Expect(repo.Changelog == Header + "\n## [26.10.18] - 2026-10-18\n\n### Added\n- C. #4\n- A. #1\n- B. #3\n\n"
        + "### Fixed\n- F. #2\n\n" + PreviousBlock, repo.Changelog);
});

Test("compile without fragments is a no-op", () =>
{
    using var repo = new TempRepo(Header + "\n" + PreviousBlock);
    var result = repo.Run();
    Expect(result.ExitCode == 0 && result.Output.Contains("No changelog fragments to compile."), result.Output);
    Expect(repo.Changelog == Header + "\n" + PreviousBlock, "CHANGELOG.md changed");
});

Test("compile refuses invalid fragments and leaves everything in place", () =>
{
    using var repo = new TempRepo(Header + "\n" + PreviousBlock);
    repo.Fragment("1-good.md", "---\ntype: Added\n---\nAdd it. #1\n");
    repo.Fragment("2-bad.md", "---\ntype: added\n---\nAdd it. #2\n");
    var result = repo.Run();
    Expect(result.ExitCode == 1, $"exit code {result.ExitCode}");
    Expect(repo.Changelog == Header + "\n" + PreviousBlock, "CHANGELOG.md changed");
    Expect(repo.FragmentNames.Count == 3, "fragments were deleted"); // two fragments plus .gitkeep
});

Test("an [Unreleased] block is rejected", () =>
{
    using var repo = new TempRepo(Header + "\n## [Unreleased]\n\n" + PreviousBlock);
    var result = repo.Run("--check");
    Expect(result.ExitCode == 1 && result.Output.Contains("must not contain an '## [Unreleased]' block"), result.Output);
});

Test("version uses CalVer without leading zeros and works on a changelog without blocks", () =>
{
    using var repo = new TempRepo(Header);
    repo.Fragment("7-add.md", "---\ntype: Added\n---\nAdd seven. #7\n");
    var result = repo.Run("--date", "2027-01-05");
    Expect(result.ExitCode == 0, $"exit code {result.ExitCode}: {result.Output}");
    Expect(repo.Changelog == Header + "\n## [27.1.5] - 2027-01-05\n\n### Added\n- Add seven. #7\n", repo.Changelog);
});

Console.WriteLine(failures.Count == 0 ? "All CompileChangelog tests passed." : $"{failures.Count} test(s) failed.");
return failures.Count == 0 ? 0 : 1;

void Test(string name, Action body)
{
    try
    {
        body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add(name);
        Console.WriteLine($"FAIL {name}\n  {exception.Message}");
    }
}

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

sealed class TempRepo : IDisposable
{
    static readonly string Script = Path.Combine(ScriptDirectory(), "CompileChangelog.cs");

    public TempRepo(string changelog)
    {
        Root = Directory.CreateTempSubdirectory("changelog-test-").FullName;
        Directory.CreateDirectory(FragmentDirectory);
        File.WriteAllText(Path.Combine(FragmentDirectory, ".gitkeep"), "");
        File.WriteAllText(Path.Combine(Root, "CHANGELOG.md"), changelog);
    }

    public string Root { get; }
    string FragmentDirectory => Path.Combine(Root, "changelog", "unreleased");
    public string Changelog => File.ReadAllText(Path.Combine(Root, "CHANGELOG.md"));
    public List<string> FragmentNames => [.. Directory.GetFiles(FragmentDirectory).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal)];

    public void Fragment(string name, string content) => File.WriteAllText(Path.Combine(FragmentDirectory, name), content);

    public (int ExitCode, string Output) Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in (string[])["run", "--file", Script, "--", "--root", Root, "--date", "2026-10-18", .. arguments])
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment.Remove("GITHUB_ACTIONS");
        using var process = Process.Start(startInfo)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr.Result);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
