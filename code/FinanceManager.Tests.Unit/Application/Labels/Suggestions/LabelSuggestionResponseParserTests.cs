using FinanceManager.Application.Labels.Suggestions;

namespace FinanceManager.Tests.Unit.Application.Labels.Suggestions;

[Collection("Application")]
[Trait("Category", "Unit")]
public class LabelSuggestionResponseParserTests
{
    [Theory]
    [InlineData("""{"suggestions":[{"labelName":" Travel "}]}""")]
    [InlineData("""Prose ```json {"SUGGESTIONS":[{"LABELNAME":" Travel "}]} ```""")]
    public void Parse_PreservesRawNamesAndRecoversWrappedJson(string content)
    {
        var item = Assert.Single(LabelSuggestionResponseParser.Parse(content));
        Assert.Equal(" Travel ", item.LabelName);
        Assert.Null(item.Rationale);
        Assert.Null(item.ExampleDescriptions);
    }

    [Fact]
    public void Parse_PopulatedFieldsAndMultipleItems_DeserializesCaseInsensitively()
    {
        var content = """
            Here are suggestions:
            {
              "Suggestions": [
                {
                  "labelName": "Dining",
                  "rationale": "Frequent restaurant charges",
                  "exampleDescriptions": ["Bistro Cafe", "Pizza Place"]
                },
                {
                  "LABELNAME": "Transport",
                  "RATIONALE": "Commute tickets",
                  "EXAMPLEDESCRIPTIONS": ["Metro Pass"]
                }
              ]
            }
            """;

        var items = LabelSuggestionResponseParser.Parse(content);

        Assert.Equal(2, items.Count);
        Assert.Equal("Dining", items[0].LabelName);
        Assert.Equal("Frequent restaurant charges", items[0].Rationale);
        Assert.Equal(["Bistro Cafe", "Pizza Place"], items[0].ExampleDescriptions);
        Assert.Equal("Transport", items[1].LabelName);
        Assert.Equal("Commute tickets", items[1].Rationale);
        Assert.Equal(["Metro Pass"], items[1].ExampleDescriptions);
    }

    [Fact]
    public void Parse_NullSuggestionEntries_AreSkipped()
    {
        var content = """{"suggestions":[null,{"labelName":"Groceries","rationale":"Supermarkets","exampleDescriptions":["Market"]}]}""";

        var item = Assert.Single(LabelSuggestionResponseParser.Parse(content));
        Assert.Equal("Groceries", item.LabelName);
        Assert.Equal("Supermarkets", item.Rationale);
        Assert.Equal(["Market"], item.ExampleDescriptions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("{broken}")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"suggestions":null}""")]
    [InlineData("""{"suggestions":42}""")]
    public void Parse_InvalidOrMissingSuggestionsReturnsEmpty(string content)
    {
        Assert.Empty(LabelSuggestionResponseParser.Parse(content));
    }
}