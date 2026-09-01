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
    [Fact]
    public void CardScopeValidation_RejectsBalancedButMisallocatedTooltips()
    {
        const string razorText = """
            <MudCard>
                <CardInfoTooltip Text="First" />
                <CardInfoTooltip Text="Duplicate" />
            </MudCard>
            <MudCard></MudCard>
            <MudCard></MudCard>
            <CardInfoTooltip Text="Orphan" />
            """;

        var mismatches = FindCardCoverageMismatches(razorText);

        Assert.Equal(4, mismatches.Count);
        Assert.Contains("card 1", mismatches[0]);
        Assert.Contains("card 2", mismatches[1]);
        Assert.Contains("card 3", mismatches[2]);
        Assert.Contains("outside a MudCard", mismatches[3]);
    }

    [Fact]
    public void CardScopeValidation_IgnoresCommentedTooltips()
    {
        const string razorText = """
            <MudCard>
                @* <CardInfoTooltip Text="Commented Razor tooltip" /> *@
            </MudCard>
            <MudCard>
                <!-- <CardInfoTooltip Text="Commented HTML tooltip" /> -->
                <CardInfoTooltip Text="Rendered" />
            </MudCard>
            """;

        var mismatches = FindCardCoverageMismatches(razorText);

        Assert.Single(mismatches);
        Assert.Contains("card 1", mismatches[0]);
    }

    [Fact]
    public void EveryMudCard_HasExactlyOneCardInfoTooltip()
    {
        var componentsRoot = GetComponentsRoot();
        var razorFiles = Directory.EnumerateFiles(componentsRoot, "*.razor", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        Assert.True(razorFiles.Count > 0, $"No Razor files found under {componentsRoot} — path resolution is wrong.");

        List<string> mismatches = [];
        var cardTotal = 0;
        var tooltipTotal = 0;

        foreach (var file in razorFiles)
        {
            var text = File.ReadAllText(file);
            var coverage = AnalyzeCardCoverage(text);
            cardTotal += coverage.Cards.Count;
            tooltipTotal += coverage.TooltipCount;
            if (coverage.Mismatches.Count > 0)
            {
                var relative = Path.GetRelativePath(componentsRoot, file);
                mismatches.AddRange(coverage.Mismatches.Select(mismatch => $"{relative}: {mismatch}"));
            }
        }

        Assert.True(cardTotal > 0, "No MudCard tags found — path resolution is wrong or all cards were removed?");
        Assert.True(tooltipTotal > 0, "No CardInfoTooltip usages found — the affordance was removed?");
        Assert.Empty(mismatches);
    }

    private static CardCoverage AnalyzeCardCoverage(string razorText)
    {
        var markup = RemoveCommentsPreservingLineNumbers(razorText);
        var cards = new List<CardSurface>();
        var openCards = new Stack<CardSurface>();
        var tooltipTotal = 0;
        var orphanTooltipTotal = 0;

        foreach (Match token in _cardMarkupTokenPattern.Matches(markup))
        {
            if (token.Value.StartsWith("</MudCard", StringComparison.Ordinal))
            {
                if (openCards.Count > 0)
                {
                    openCards.Pop();
                }

                continue;
            }

            if (token.Value.StartsWith("<CardInfoTooltip", StringComparison.Ordinal))
            {
                tooltipTotal++;
                if (openCards.TryPeek(out var card))
                {
                    card.TooltipCount++;
                }
                else
                {
                    orphanTooltipTotal++;
                }

                continue;
            }

            var cardSurface = new CardSurface(GetLineNumber(markup, token.Index));
            cards.Add(cardSurface);
            if (!token.Value.TrimEnd().EndsWith("/>", StringComparison.Ordinal))
            {
                openCards.Push(cardSurface);
            }
        }

        List<string> mismatches = [];
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            if (card.TooltipCount != 1)
            {
                mismatches.Add($"card {index + 1} at line {card.LineNumber}: {card.TooltipCount} CardInfoTooltip");
            }
        }

        if (orphanTooltipTotal > 0)
        {
            mismatches.Add($"{orphanTooltipTotal} CardInfoTooltip outside a MudCard");
        }

        return new CardCoverage(cards, tooltipTotal, mismatches);
    }

    private static string RemoveCommentsPreservingLineNumbers(string text) =>
        _commentPattern.Replace(text, match => new string(match.Value.Select(character =>
            character is '\n' or '\r' ? character : ' ').ToArray()));

    private static IReadOnlyList<string> FindCardCoverageMismatches(string razorText) =>
        AnalyzeCardCoverage(razorText).Mismatches;

    private static int GetLineNumber(string text, int index)
    {
        var lineNumber = 1;
        for (var position = 0; position < index; position++)
        {
            if (text[position] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static string GetComponentsRoot()
    {
        // The test assembly runs from code/FinanceManager.Tests.Unit/bin/<config>/<tfm>/.
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "FinanceManager.Components"));
        return root;
    }

    private static readonly Regex _cardMarkupTokenPattern = new(
        @"</MudCard\s*>|<MudCard(?=[\s>/])[^>]*>|<CardInfoTooltip(?=[\s>/])",
        RegexOptions.Compiled);

    private static readonly Regex _commentPattern = new(
        @"@\*[\s\S]*?\*@|<!--[\s\S]*?-->",
        RegexOptions.Compiled);

    private sealed class CardSurface(int lineNumber)
    {
        public int LineNumber { get; } = lineNumber;

        public int TooltipCount { get; set; }
    }

    private sealed record CardCoverage(
        IReadOnlyList<CardSurface> Cards,
        int TooltipCount,
        IReadOnlyList<string> Mismatches);
}