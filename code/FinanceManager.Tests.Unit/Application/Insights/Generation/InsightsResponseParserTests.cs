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
        var content = """{"insights":[{"title":"Save more","message":"You spent a lot","tags":["spending","alert"]},{"title":"Nice","message":"Good month"}]}""";

        var result = _parser.Parse(content);

        Assert.Equal(2, result.Count);
        Assert.Equal("Save more", result[0].Title);
        Assert.Equal("You spent a lot", result[0].Message);
        Assert.Equal(["spending", "alert"], result[0].Tags);
        Assert.Equal("Nice", result[1].Title);
        Assert.Null(result[1].Tags);
    }

    [Fact]
    public void Parse_JsonWrappedInProse_ExtractsEmbeddedObject()
    {
        var content = """Here is the result: {"insights":[{"title":"T","message":"M"}]} hope it helps!""";

        var result = _parser.Parse(content);

        var insight = Assert.Single(result);
        Assert.Equal("T", insight.Title);
        Assert.Equal("M", insight.Message);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"unexpected\":true}")]
    [InlineData("[]")]
    public void Parse_InvalidOrMismatchedJson_ReturnsEmpty(string content)
    {
        Assert.Empty(_parser.Parse(content));
    }

    [Fact]
    public void Parse_MissingFields_ReturnsNullValues()
    {
        var content = """{"insights":[{"title":"Only title"},{"message":"Only message"}]}""";

        var result = _parser.Parse(content);

        Assert.Equal(2, result.Count);
        Assert.Equal("Only title", result[0].Title);
        Assert.Null(result[0].Message);
        Assert.Null(result[1].Title);
        Assert.Equal("Only message", result[1].Message);
    }
}