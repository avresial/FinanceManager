using FinanceManager.Application.Insights.Generation;

namespace FinanceManager.Tests.Unit.Application.Insights.Generation;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InsightsResponseParserTests
{
    private readonly InsightsResponseParser _parser = new();

    [Fact]
    public void Parse_ValidJson_ReturnsInsights()
    {
        var content = """{"insights":[{"title":"Spending up","message":"Groceries rose 10%.","tags":["spending"]},{"title":"Savings","message":"You saved more.","tags":["savings"]}]}""";

        var result = _parser.Parse(content);

        Assert.Equal(2, result.Count);
        Assert.Equal("Spending up", result[0].Title);
        Assert.Equal("Groceries rose 10%.", result[0].Message);
        Assert.Equal(["spending"], result[0].Tags);
        Assert.Equal("Savings", result[1].Title);
    }

    [Fact]
    public void Parse_JsonWrappedInProse_ExtractsEmbeddedObject()
    {
        var content = """Here is the result: {"insights":[{"title":"T","message":"M","tags":[]}]} hope it helps!""";

        var result = _parser.Parse(content);

        var insight = Assert.Single(result);
        Assert.Equal("T", insight.Title);
        Assert.Equal("M", insight.Message);
    }

    [Fact]
    public void Parse_MissingInsightsProperty_ReturnsEmpty()
    {
        var result = _parser.Parse("""{"items":[{"title":"T","message":"M"}]}""");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmpty()
    {
        var result = _parser.Parse("not json at all");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_CaseInsensitivePropertyNames_ReturnsInsights()
    {
        var content = """{"Insights":[{"Title":"T","Message":"M","Tags":["a"]}]}""";

        var result = _parser.Parse(content);

        var insight = Assert.Single(result);
        Assert.Equal("T", insight.Title);
    }
}