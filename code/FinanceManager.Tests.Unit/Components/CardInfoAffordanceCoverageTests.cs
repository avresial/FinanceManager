using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Components;

/// <summary>
/// Guards the information-affordance convention: every <c>MudCard</c> surface hosts exactly one
/// <c>CardInfoTooltip</c>. The Razor sources are parsed by tag order, so every tooltip is matched
/// to its containing card and an orphan affordance or a card without its information affordance
/// fails the build.
/// </summary>
[Trait("Category", "Unit")]
public class CardInfoAffordanceCoverageTests
{
    private static readonly Regex CardAndTooltipTagPattern = new(
        "</?MudCard(?=[\\s>/])[^>]*>|<CardInfoTooltip(?=[\\s>/])[^>]*>",
        RegexOptions.Compiled);

    [Fact]
    public void EveryMudCard_HasExactlyOneCardInfoTooltip()
    {
        var componentsRoot = GetComponentsRoot();
        var razorFiles = Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        Assert.True(razorFiles.Count > 0, $"No Razor files found under {componentsRoot} — path resolution is wrong.");

        var mismatches = new List<string>();
        var tooltipTotal = 0;

        foreach (var file in razorFiles)
        {
            var text = File.ReadAllText(file);
            tooltipTotal += CountTags(text, "CardInfoTooltip");
            var fileMismatches = FindAffordanceMismatches(text);
            if (fileMismatches.Count > 0)
            {
                var relative = Path.GetRelativePath(componentsRoot, file);
                mismatches.Add($"{relative}: {string.Join("; ", fileMismatches)}");
            }
        }

        Assert.True(tooltipTotal > 0, "No CardInfoTooltip usages found — the affordance was removed?");
        Assert.Empty(mismatches);
    }

    [Fact]
    public void FindAffordanceMismatches_ReportsOrphanAndMissingTooltip_WhenFileTotalsMatch()
    {
        const string razor = "<MudCard><CardInfoTooltip /></MudCard><MudCard></MudCard><CardInfoTooltip />";

        var mismatches = FindAffordanceMismatches(razor);

        Assert.Contains("MudCard #2 contains 0 CardInfoTooltip instances", mismatches);
        Assert.Contains("1 orphan CardInfoTooltip instance", mismatches);
    }

    private static List<string> FindAffordanceMismatches(string razorText)
    {
        var tooltipCounts = new List<int>();
        var openCards = new Stack<int>();
        var orphanTooltipCount = 0;

        foreach (Match match in CardAndTooltipTagPattern.Matches(razorText))
        {
            if (match.Value.StartsWith("</", StringComparison.Ordinal))
            {
                if (openCards.Count > 0)
                {
                    openCards.Pop();
                }

                continue;
            }

            if (match.Value.StartsWith("<MudCard", StringComparison.Ordinal))
            {
                tooltipCounts.Add(0);
                openCards.Push(tooltipCounts.Count - 1);

                if (match.Value.EndsWith("/>", StringComparison.Ordinal))
                {
                    openCards.Pop();
                }

                continue;
            }

            if (openCards.TryPeek(out var cardIndex))
            {
                tooltipCounts[cardIndex]++;
            }
            else
            {
                orphanTooltipCount++;
            }
        }

        var mismatches = tooltipCounts
            .Select((tooltipCount, index) => new { tooltipCount, index })
            .Where(card => card.tooltipCount != 1)
            .Select(card => $"MudCard #{card.index + 1} contains {card.tooltipCount} CardInfoTooltip instances")
            .ToList();

        if (orphanTooltipCount > 0)
        {
            mismatches.Add($"{orphanTooltipCount} orphan CardInfoTooltip instance{(orphanTooltipCount == 1 ? string.Empty : "s")}");
        }

        return mismatches;
    }

    // Matches component opening tags (<MudCard, <MudCard ...>, <MudCard/>) while ignoring
    // closing tags and look-alike tags such as <MudCardHeader>.
    private static int CountTags(string razorText, string tagName)
    {
        var pattern = new Regex($"<{tagName}(?=[\\s>/])", RegexOptions.Compiled);
        return pattern.Matches(razorText).Count;
    }

    private static string GetComponentsRoot()
    {
        // The test assembly runs from code/FinanceManager.Tests.Unit/bin/<config>/<tfm>/.
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "FinanceManager.Components"));
        return root;
    }
}