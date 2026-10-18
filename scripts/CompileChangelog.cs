// Validates changelog fragments (changelog/unreleased/*.md) and compiles them into CHANGELOG.md.
// See changelog/README.md for the fragment format.
//
// Usage (from the repository root):
//   dotnet run scripts/CompileChangelog.cs -- --check    validate fragments, change nothing
//   dotnet run scripts/CompileChangelog.cs               fold fragments into today's block, delete them
// Options: --root <dir> (repository root, default: current directory),
//          --date <yyyy-MM-dd> (release date, default: today in UTC, matching the build's CalVer).
using System.Globalization;
using System.Text.RegularExpressions;

var check = false;
var root = Directory.GetCurrentDirectory();
var date = DateOnly.FromDateTime(DateTime.UtcNow);

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--check":
            check = true;
            break;
        case "--root" when i + 1 < args.Length:
            root = args[++i];
            break;
        case "--date" when i + 1 < args.Length:
            if (!DateOnly.TryParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return Usage($"--date expects yyyy-MM-dd, got '{args[i]}'.");
            break;
        default:
            return Usage($"Unknown or incomplete argument '{args[i]}'.");
    }
}

var fragmentDirectory = Path.Combine(root, "changelog", "unreleased");
var changelogPath = Path.Combine(root, "CHANGELOG.md");
if (!Directory.Exists(fragmentDirectory) || !File.Exists(changelogPath))
    return Usage($"Expected CHANGELOG.md and changelog/unreleased/ under '{Path.GetFullPath(root)}'. Run from the repository root or pass --root.");

var files = Directory.GetFiles(fragmentDirectory)
    .Where(path => !Path.GetFileName(path).StartsWith('.')) // .gitkeep, .DS_Store
    .Order(StringComparer.Ordinal)
    .ToList();

var errors = new List<(string Path, string Message)>();
var fragments = new List<Fragment>();
foreach (var file in files)
{
    var fileErrors = new List<string>();
    var fragment = Changelog.ParseFragment(Path.GetFileName(file), File.ReadAllText(file), fileErrors);
    errors.AddRange(fileErrors.Select(message => (Path.GetRelativePath(root, file).Replace('\\', '/'), message)));
    if (fragment is not null)
        fragments.Add(fragment);
}

string compiled;
try
{
    // Compiling in memory during --check too catches CHANGELOG.md problems before they reach develop.
    compiled = Changelog.Compile(File.ReadAllText(changelogPath), fragments, date);
}
catch (InvalidOperationException exception)
{
    errors.Add(("CHANGELOG.md", exception.Message));
    compiled = "";
}

if (errors.Count > 0)
{
    var inGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    foreach (var (path, message) in errors)
        Console.Error.WriteLine(inGitHubActions ? $"::error file={path}::{message}" : $"error: {path}: {message}");
    Console.Error.WriteLine($"{errors.Count} changelog error(s). See changelog/README.md for the fragment format.");
    return 1;
}

if (check)
{
    Console.WriteLine($"{fragments.Count} changelog fragment(s) valid.");
    return 0;
}

if (fragments.Count == 0)
{
    Console.WriteLine("No changelog fragments to compile.");
    return 0;
}

File.WriteAllText(changelogPath, compiled);
foreach (var file in files)
    File.Delete(file);
Console.WriteLine($"Compiled {fragments.Count} fragment(s) into {Changelog.Heading(date)}.");
return 0;

static int Usage(string message)
{
    Console.Error.WriteLine($"error: {message}");
    Console.Error.WriteLine("usage: dotnet run scripts/CompileChangelog.cs -- [--check] [--root <dir>] [--date <yyyy-MM-dd>]");
    return 2;
}

record Fragment(string FileName, int? Issue, string Type, string Entry);

static class Changelog
{
    // Keep a Changelog sections, in the order they appear inside a version block.
    public static readonly string[] Sections = ["Added", "Changed", "Deprecated", "Removed", "Fixed", "Security"];

    static readonly Regex FileNamePattern = new(@"^(?:(?<issue>[0-9]+)-)?[a-z0-9]+(?:-[a-z0-9]+)*\.md$");
    static readonly Regex FrontMatterLine = new(@"^(?<key>[A-Za-z_]+)\s*:\s*(?<value>.*)$");
    static readonly Regex HtmlTag = new(@"<[A-Za-z/!][^>]*>");
    static readonly Regex CodeSpan = new(@"`[^`]*`");
    static readonly Regex TrailingIssue = new(@"#[0-9]+$");

    public static string Heading(DateOnly date) =>
        $"## [{date:yy}.{date.Month}.{date.Day}] - {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    public static Fragment? ParseFragment(string fileName, string content, List<string> errors)
    {
        var errorCount = errors.Count;
        var nameMatch = FileNamePattern.Match(fileName);
        if (!nameMatch.Success)
            errors.Add("File name must be lower-case kebab-case ending in .md, e.g. '123-short-issue-title.md'.");
        int? issue = nameMatch.Groups["issue"].Success ? int.Parse(nameMatch.Groups["issue"].Value, CultureInfo.InvariantCulture) : null;

        var lines = content.TrimStart('﻿').ReplaceLineEndings("\n").Split('\n');
        var close = lines.Length > 0 && lines[0].Trim() == "---" ? Array.FindIndex(lines, 1, line => line.Trim() == "---") : -1;
        if (close < 0)
        {
            errors.Add("Fragment must start with YAML front matter: a '---' line, 'type: <Section>', and a closing '---' line.");
            return null;
        }

        string? type = null;
        foreach (var line in lines[1..close].Where(line => line.Trim().Length > 0))
        {
            var match = FrontMatterLine.Match(line.Trim());
            if (!match.Success)
                errors.Add($"Invalid front matter line '{line.Trim()}'; expected 'type: <Section>'.");
            else if (match.Groups["key"].Value != "type")
                errors.Add($"Unknown front matter key '{match.Groups["key"].Value}'; only 'type' is allowed.");
            else
                type = match.Groups["value"].Value.Trim().Trim('"', '\'');
        }

        var allowed = string.Join(", ", Sections);
        if (type is null)
            errors.Add($"Front matter is missing 'type:'; use one of {allowed}.");
        else if (!Sections.Contains(type))
            errors.Add($"Type '{type}' is not allowed; use one of {allowed} (case-sensitive).");

        var body = lines[(close + 1)..].Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (body.Count != 1)
        {
            errors.Add($"Fragment body must be exactly one non-empty entry line after the front matter; found {body.Count}.");
            return null;
        }

        // A leading list marker is optional; the compiler adds it.
        var entry = body[0].StartsWith("- ", StringComparison.Ordinal) ? body[0][2..].Trim() : body[0];
        if (entry.Length == 0 || entry.StartsWith('#'))
            errors.Add("Entry must be a sentence, not empty and not a Markdown heading.");
        if (HtmlTag.IsMatch(CodeSpan.Replace(entry, "")))
            errors.Add("Entry contains HTML; use Markdown only.");
        if (issue is int number && !entry.EndsWith($"#{number}", StringComparison.Ordinal))
            errors.Add($"Entry must end with '#{number}' to match the file name's issue number.");
        if (issue is null && nameMatch.Success && TrailingIssue.IsMatch(entry))
            errors.Add("Entry references an issue but the file name does not; prefix the file name with the issue number, e.g. '123-short-issue-title.md'.");

        return errors.Count == errorCount ? new Fragment(fileName, issue, type!, entry) : null;
    }

    // Folds fragments into the block for `date` at the top of the changelog, creating it when missing.
    // New entries go above existing ones in each section (newest first); exact duplicates are skipped.
    public static string Compile(string changelog, IReadOnlyList<Fragment> fragments, DateOnly date)
    {
        var lines = changelog.TrimStart('﻿').ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').ToList();
        var heading = Heading(date);
        var blockStart = lines.FindIndex(line => line.StartsWith("## ", StringComparison.Ordinal));
        if (blockStart >= 0 && lines[blockStart].StartsWith("## [Unreleased]", StringComparison.Ordinal))
            throw new InvalidOperationException("CHANGELOG.md must not contain an '## [Unreleased]' block; pending changes live in changelog/unreleased/.");
        if (fragments.Count == 0)
            return changelog;

        var entries = Sections.ToDictionary(section => section, _ => new List<string>());
        var blockEnd = blockStart < 0 ? lines.Count : blockStart;
        if (blockStart >= 0 && lines[blockStart].TrimEnd() == heading)
        {
            blockEnd = lines.FindIndex(blockStart + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
            if (blockEnd < 0)
                blockEnd = lines.Count;

            string? section = null;
            foreach (var line in lines[(blockStart + 1)..blockEnd].Where(line => line.Trim().Length > 0))
            {
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    section = line[4..].Trim();
                    if (!entries.ContainsKey(section))
                        throw new InvalidOperationException($"Block '{heading}' has unknown section '{line}'.");
                }
                else if (section is null)
                    throw new InvalidOperationException($"Block '{heading}' has content before its first '### ' section.");
                else
                    entries[section].Add(line.TrimEnd());
            }
        }
        else if (blockStart < 0)
            blockStart = blockEnd;

        var ordered = fragments.OrderByDescending(fragment => fragment.Issue ?? -1).ThenBy(fragment => fragment.FileName, StringComparer.Ordinal);
        foreach (var group in ordered.GroupBy(fragment => fragment.Type))
        {
            var additions = group.Select(fragment => $"- {fragment.Entry}").Distinct().Except(entries[group.Key]);
            entries[group.Key].InsertRange(0, additions);
        }

        var block = new List<string> { heading, "" };
        foreach (var section in Sections.Where(section => entries[section].Count > 0))
            block.AddRange([$"### {section}", .. entries[section], ""]);

        var before = lines[..blockStart];
        while (before.Count > 0 && before[^1].Trim().Length == 0)
            before.RemoveAt(before.Count - 1);
        var after = lines[blockEnd..];
        if (after.Count == 0)
            block.RemoveAt(block.Count - 1);

        return string.Join("\n", [.. before, "", .. block, .. after]) + "\n";
    }
}
