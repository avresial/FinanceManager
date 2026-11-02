using FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared.Import;

[Trait("Category", "Unit")]
public class ImportStepCaptionTests
{
    private static readonly string[] _steps = ["Select file", "Map columns", "Review"];

    [Theory]
    [InlineData(0, "Step 1 of 3 · Map columns next")]
    [InlineData(1, "Step 2 of 3 · Review next")]
    [InlineData(2, "Step 3 of 3")]
    public void Text_NamesThePositionAndTheNextStep(int activeIndex, string expected) =>
        Assert.Equal(expected, ImportStepCaption.Text(activeIndex, _steps));

    [Fact]
    public void Text_IsEmptyWithoutSteps() => Assert.Equal(string.Empty, ImportStepCaption.Text(0, []));
}