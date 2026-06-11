using FinanceManager.Application.Insights.Generation;

namespace FinanceManager.Tests.Unit.Application.Insights.Generation;

[Collection("Application")]
[Trait("Category", "Unit")]
public class FinancialInsightNormalizerTests
{
    private const int _userId = 7;

    private readonly FinancialInsightNormalizer _normalizer = new();

    [Fact]
    public void Normalize_TrimsAndCopiesFields()
    {
        var parsed = new List<InsightItem> { new("  Title  ", "  Message  ", ["Spending"]) };

        var result = _normalizer.Normalize(parsed, _userId, accountId: 3, count: 5);

        var insight = Assert.Single(result);
        Assert.Equal(_userId, insight.UserId);
        Assert.Equal(3, insight.AccountId);
        Assert.Equal("Title", insight.Title);
        Assert.Equal("Message", insight.Message);
        Assert.Equal("spending", insight.Tags);
    }

    [Fact]
    public void Normalize_SkipsItemsWithEmptyTitleOrMessage()
    {
        var parsed = new List<InsightItem>
        {
            new("", "Message", null),
            new("Title", "   ", null),
            new("Good", "Good", null)
        };

        var result = _normalizer.Normalize(parsed, _userId, accountId: null, count: 10);

        var insight = Assert.Single(result);
        Assert.Equal("Good", insight.Title);
    }

    [Fact]
    public void Normalize_NoTags_DefaultsToSummary()
    {
        var parsed = new List<InsightItem> { new("Title", "Message", null) };

        var result = _normalizer.Normalize(parsed, _userId, accountId: null, count: 1);

        Assert.Equal("summary", Assert.Single(result).Tags);
    }

    [Fact]
    public void Normalize_TagsAreLoweredDistinctAndCappedAtThree()
    {
        var parsed = new List<InsightItem>
        {
            new("Title", "Message", ["Food", "FOOD", "Travel", "Health", "Fun"])
        };

        var result = _normalizer.Normalize(parsed, _userId, accountId: null, count: 1);

        Assert.Equal("food,travel,health", Assert.Single(result).Tags);
    }

    [Fact]
    public void Normalize_RespectsCount()
    {
        var parsed = new List<InsightItem>
        {
            new("A", "A", null),
            new("B", "B", null),
            new("C", "C", null)
        };

        var result = _normalizer.Normalize(parsed, _userId, accountId: null, count: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Title);
        Assert.Equal("B", result[1].Title);
    }

    [Fact]
    public void Normalize_TruncatesOverlongTitleAndMessage()
    {
        var parsed = new List<InsightItem>
        {
            new(new string('t', 200), new string('m', 2000), null)
        };

        var result = _normalizer.Normalize(parsed, _userId, accountId: null, count: 1);

        var insight = Assert.Single(result);
        Assert.Equal(128, insight.Title.Length);
        Assert.Equal(1024, insight.Message.Length);
    }
}