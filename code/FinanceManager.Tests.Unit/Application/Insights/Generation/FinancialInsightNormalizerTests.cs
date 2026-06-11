using FinanceManager.Application.Insights.Generation;

namespace FinanceManager.Tests.Unit.Application.Insights.Generation;

[Collection("Application")]
[Trait("Category", "Unit")]
public class FinancialInsightNormalizerTests
{
    private readonly FinancialInsightNormalizer _normalizer = new();

    [Fact]
    public void Normalize_ValidItems_MapsUserAccountAndContent()
    {
        List<InsightItem> items = [new("Title", "Message", ["Spending"])];

        var result = _normalizer.Normalize(items, userId: 7, accountId: 3, count: 5);

        var insight = Assert.Single(result);
        Assert.Equal(7, insight.UserId);
        Assert.Equal(3, insight.AccountId);
        Assert.Equal("Title", insight.Title);
        Assert.Equal("Message", insight.Message);
        Assert.Equal("spending", insight.Tags);
        Assert.NotEqual(default, insight.CreatedAt);
    }

    [Fact]
    public void Normalize_MoreItemsThanCount_TakesOnlyCount()
    {
        List<InsightItem> items = [new("A", "M", null), new("B", "M", null), new("C", "M", null)];

        var result = _normalizer.Normalize(items, userId: 1, accountId: null, count: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Title);
        Assert.Equal("B", result[1].Title);
    }

    [Theory]
    [InlineData(null, "Message")]
    [InlineData("  ", "Message")]
    [InlineData("Title", null)]
    [InlineData("Title", "  ")]
    public void Normalize_MissingTitleOrMessage_SkipsItem(string? title, string? message)
    {
        List<InsightItem> items = [new(title, message, null)];

        var result = _normalizer.Normalize(items, userId: 1, accountId: null, count: 5);

        Assert.Empty(result);
    }

    [Fact]
    public void Normalize_LongTitleAndMessage_Truncates()
    {
        List<InsightItem> items = [new(new string('t', 200), new string('m', 2000), null)];

        var result = _normalizer.Normalize(items, userId: 1, accountId: null, count: 1);

        var insight = Assert.Single(result);
        Assert.Equal(128, insight.Title.Length);
        Assert.Equal(1024, insight.Message.Length);
    }

    [Fact]
    public void Normalize_NoTags_DefaultsToSummary()
    {
        List<InsightItem> items = [new("T", "M", null), new("T", "M", ["  ", ""])];

        var result = _normalizer.Normalize(items, userId: 1, accountId: null, count: 5);

        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal("summary", i.Tags));
    }

    [Fact]
    public void Normalize_Tags_AreLowercasedDeduplicatedAndCapped()
    {
        List<InsightItem> items = [new("T", "M", ["Spending", "spending", "SAVINGS", "budget", "extra"])];

        var result = _normalizer.Normalize(items, userId: 1, accountId: null, count: 1);

        var insight = Assert.Single(result);
        Assert.Equal("spending,savings,budget", insight.Tags);
    }
}