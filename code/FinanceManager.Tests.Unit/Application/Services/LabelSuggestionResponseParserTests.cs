using FinanceManager.Application.Labels.Suggestions;

namespace FinanceManager.Tests.Unit.Application.Services;

public class LabelSuggestionResponseParserTests
{
    [Theory]
    [InlineData("{\"suggestions\":[{\"labelName\":\" Travel \"}]}")]
    [InlineData("Prose ```json {\"SUGGESTIONS\":[{\"LABELNAME\":\" Travel \"}]} ```")]
    public void Parse_PreservesRawNamesAndRecoversWrappedJson(string content)
    {
        var item = Assert.Single(LabelSuggestionResponseParser.Parse(content));
        Assert.Equal(" Travel ", item.LabelName);
        Assert.Null(item.Rationale);
        Assert.Null(item.ExampleDescriptions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("{broken}")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"suggestions\":null}")]
    [InlineData("{\"suggestions\":42}")]
    public void Parse_InvalidOrMissingSuggestionsReturnsEmpty(string content)
    {
        Assert.Empty(LabelSuggestionResponseParser.Parse(content));
    }
}