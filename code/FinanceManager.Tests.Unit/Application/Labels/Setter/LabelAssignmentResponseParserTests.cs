using FinanceManager.Application.Labels.Setter;

namespace FinanceManager.Tests.Unit.Application.Labels.Setter;

[Collection("Application")]
[Trait("Category", "Unit")]
public class LabelAssignmentResponseParserTests
{
    private readonly LabelAssignmentResponseParser _parser = new();

    [Fact]
    public void Parse_ValidJson_ReturnsAssignments()
    {
        var content = """{"assignments":[{"entryId":1,"labelName":"Groceries"},{"entryId":2,"labelName":"Rent"}]}""";

        var result = _parser.Parse(content);

        Assert.Equal(2, result.Count);
        Assert.Equal(new LabelAssignment(1, "Groceries"), result[0]);
        Assert.Equal(new LabelAssignment(2, "Rent"), result[1]);
    }

    [Fact]
    public void Parse_JsonWrappedInProse_ExtractsEmbeddedObject()
    {
        var content = """Here is the result: {"assignments":[{"entryId":7,"labelName":"Utilities"}]} hope it helps!""";

        var result = _parser.Parse(content);

        var assignment = Assert.Single(result);
        Assert.Equal(new LabelAssignment(7, "Utilities"), assignment);
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
        var content = """{"assignments":[{"entryId":3},{"labelName":"Rent"}]}""";

        var result = _parser.Parse(content);

        Assert.Equal(2, result.Count);
        Assert.Equal(new LabelAssignment(3, null), result[0]);
        Assert.Equal(new LabelAssignment(null, "Rent"), result[1]);
    }
}