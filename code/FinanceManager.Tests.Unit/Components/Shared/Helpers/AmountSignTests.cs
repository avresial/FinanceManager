using FinanceManager.Components.Shared.Helpers;
using MudBlazor;

namespace FinanceManager.Tests.Unit.Components.Shared.Helpers;

[Trait("Category", "Unit")]
public class AmountSignTests
{
    [Theory]
    [InlineData(5000.0, Color.Success, "+")]
    [InlineData(-1000.0, Color.Error, "")]
    [InlineData(0.0, Color.Default, "")]
    public void ColorAndPrefix_FollowTheSignOfTheAmount(double amount, Color expectedColor, string expectedPrefix)
    {
        Assert.Equal(expectedColor, AmountSign.ColorFor((decimal)amount));
        Assert.Equal(expectedPrefix, AmountSign.PrefixFor((decimal)amount));
    }
}