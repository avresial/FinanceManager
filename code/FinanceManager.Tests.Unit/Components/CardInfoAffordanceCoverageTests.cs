using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Components;

/// <summary>
/// Guards the information-affordance convention: every <c>MudCard</c> surface hosts exactly one
/// <c>CardInfoTooltip</c>. The counts are parsed from the Razor sources, so formatting and
/// multiple cards per file are tolerated, but a card added without its information affordance
/// (or an orphan affordance with no card) fails the build.
/// </summary>
[Trait("Category", "Unit")]
public class CardInfoAffordanceCoverageTests
{
    [Fact]
    public void EveryMudCard_HasExactlyOneCardInfoTooltip()
    {
        var componentsRoot = GetComponentsRoot();
        var razorFiles = Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        Assert.True(razorFiles.Count > 0, $"No Razor files found under {componentsRoot} — path resolution is wrong.");

        var mismatches = new List<string>();
        var cardTotal = 0;
        var tooltipTotal = 0;

        foreach (var file in razorFiles)
        {
            var text = File.ReadAllText(file);
            var cards = CountTags(text, "MudCard");
            var tooltips = CountTags(text, "CardInfoTooltip");
            cardTotal += cards;
            tooltipTotal += tooltips;
            if (cards != tooltips)
            {
                var relative = Path.GetRelativePath(componentsRoot, file);
                mismatches.Add($"{relative}: {cards} MudCard, {tooltips} CardInfoTooltip");
            }
        }

        Assert.True(tooltipTotal > 0, "No CardInfoTooltip usages found — the affordance was removed?");
        Assert.Empty(mismatches);
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