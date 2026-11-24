using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Architecture;

/// <summary>
/// Financial accounts are gone, so no screen may talk about them. This is the grep gate for that: it scans every Razor
/// file under Components for the word "account" and only lets through sign-in and user-account wording (login, register,
/// settings, MCP connect, admin users, the user menu) and bank-statement counterparties.
/// </summary>
[Trait("Category", "Architecture")]
public partial class UserVisibleCopyTests
{
    // Folders whose "account" always means the user's own login.
    private static readonly string[] _userAccountFolders =
    [
        "Features/Identity/",
        "Features/Mcp/",
        "Features/Administration/",
    ];

    // Single files where the word is not about a financial account.
    private static readonly Dictionary<string, string> _allowedFiles = new(StringComparer.Ordinal)
    {
        ["Shared/Layout/MainLayout.razor"] = "aria-label of the user menu",
        ["Features/FinancialAccounts/Components/CurrencyAccountComponents/Import/ImportCurrencyEntriesComponent.razor"] = "a counterparty's bank account in a CSV helper text",
        ["Features/Transactions/Components/Pages/LegacyAccountRedirect.razor"] = "explains why the legacy account routes exist",
        ["Features/FinancialAccounts/Components/Shared/AccountFormContainer.razor"] = "CSS class name of the shared form frame",
    };

    [GeneratedRegex(@"\baccounts?\b", RegexOptions.IgnoreCase)]
    private static partial Regex AccountWord();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [Fact]
    public void RazorMarkup_DoesNotMentionFinancialAccounts()
    {
        var root = FindComponentsRoot();
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal)) continue;
            if (_userAccountFolders.Any(folder => relative.StartsWith(folder, StringComparison.Ordinal)) || _allowedFiles.ContainsKey(relative)) continue;

            // Blank out Razor comments but keep their line breaks so reported line numbers stay right.
            var text = RazorComment().Replace(File.ReadAllText(file), match => new string('\n', match.Value.Count(c => c == '\n')));
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                if (AccountWord().IsMatch(lines[i]))
                    hits.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
        }

        Assert.True(hits.Count == 0,
            "User-visible copy must not mention financial accounts (login wording belongs in Identity, Mcp or Administration):\n" + string.Join('\n', hits));
    }

    [Fact]
    public void AllowedFiles_StillExist()
    {
        var root = FindComponentsRoot();
        var missing = _allowedFiles.Keys.Where(path => !File.Exists(Path.Combine(root, path))).ToArray();

        Assert.True(missing.Length == 0, $"Remove these stale entries from the allow-list: {string.Join(", ", missing)}");
    }

    private static string FindComponentsRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "code", "FinanceManager.Components");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("Could not find code/FinanceManager.Components above the test output directory.");
    }
}