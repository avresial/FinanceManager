using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Architecture;

/// <summary>
/// Financial accounts are gone, so no screen may talk about them. This is the grep gate for that: it scans the Razor markup
/// under Components (comments stripped, build output skipped) for the word "account" and lets through only the exact phrases
/// below, which are about the user's own login or a bank-statement counterparty. A new phrase, even in a login or admin file,
/// fails until it is listed here. Strings in C# code-behind, dialogs built in code and Contracts are outside this check.
/// </summary>
[Trait("Category", "Architecture")]
public partial class UserVisibleCopyTests
{
    // Exact phrases (a substring of the offending line) per file; each is about the user's login or a counterparty, never a financial account.
    private static readonly Dictionary<string, string[]> _allowedPhrases = new(StringComparer.Ordinal)
    {
        ["Features/Identity/Components/ForgotPasswordComponent.razor"] = ["Enter the email for your account"],
        ["Features/Identity/Components/RegisterComponent.razor"] = ["Create your account", "Account created."],
        ["Features/Identity/Components/UserSettingsPage.razor"] =
        [
            "Manage your Finance Manager account, security, and subscription.",
            "Taken from your account.",
            "for the lifetime of your account.",
            "Permanently delete this account and all associated financial data.",
            "Delete account",
        ],
        ["Features/Identity/Components/LoginComponent.razor"] =
        [
            "Sign in to your account",
            "authorize MCP access to your Finance Manager account.",
            "Check out demo account",
        ],
        ["Features/Mcp/Components/McpConnectPage.razor"] = ["secure access to your Finance Manager account."],
        ["Features/Administration/Components/AdminDashboard.razor"] = ["All registered user accounts on the platform."],
        ["Shared/Layout/MainLayout.razor"] = ["aria-label=\"Account\""],
        ["Features/Transactions/Cash/Components/Import/ImportCurrencyEntriesComponent.razor"] = ["Counterparty name or account (optional)"],
        ["Features/Transactions/Components/TransactionFormContainer.razor"] = ["fm-account-form"],
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
            _allowedPhrases.TryGetValue(relative, out var allowed);

            // Blank out Razor comments but keep their line breaks so reported line numbers stay right.
            var text = RazorComment().Replace(File.ReadAllText(file), match => new string('\n', match.Value.Count(c => c == '\n')));
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                if (AccountWord().IsMatch(lines[i]) && !(allowed ?? []).Any(phrase => lines[i].Contains(phrase, StringComparison.Ordinal)))
                    hits.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
        }

        Assert.True(hits.Count == 0,
            "User-visible copy must not mention financial accounts (list a login or counterparty phrase exactly in _allowedPhrases):\n" + string.Join('\n', hits));
    }

    [Fact]
    public void AllowedPhrases_StillExist()
    {
        var root = FindComponentsRoot();
        var stale = _allowedPhrases
            .SelectMany(entry => entry.Value.Select(phrase => (File: entry.Key, Phrase: phrase)))
            .Where(entry => !File.Exists(Path.Combine(root, entry.File)) || !File.ReadAllText(Path.Combine(root, entry.File)).Contains(entry.Phrase, StringComparison.Ordinal))
            .Select(entry => $"{entry.File}: {entry.Phrase}")
            .ToArray();

        Assert.True(stale.Length == 0, $"Remove these stale entries from the allow-list:\n{string.Join('\n', stale)}");
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