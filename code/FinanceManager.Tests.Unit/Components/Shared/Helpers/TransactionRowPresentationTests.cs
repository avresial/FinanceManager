using FinanceManager.Components.Shared.Helpers;
using MudBlazor;

namespace FinanceManager.Tests.Unit.Components.Shared.Helpers;

[Trait("Category", "Unit")]
public class TransactionRowPresentationTests
{
    [Fact]
    public void ShouldShowTime_HidesMidnightPlaceholder() =>
        Assert.False(TransactionRowPresentation.ShouldShowTime(new DateTime(2026, 5, 1), siblingsShareSameTime: false));

    [Fact]
    public void ShouldShowTime_HidesTimeSharedByAllSiblings() =>
        Assert.False(TransactionRowPresentation.ShouldShowTime(new DateTime(2026, 5, 1, 18, 5, 0), siblingsShareSameTime: true));

    [Fact]
    public void ShouldShowTime_ShowsDistinctTime() =>
        Assert.True(TransactionRowPresentation.ShouldShowTime(new DateTime(2026, 5, 1, 18, 5, 0), siblingsShareSameTime: false));

    [Fact]
    public void AllShareSameTime_RequiresMoreThanOneRowWithIdenticalTime()
    {
        var same = new[] { new DateTime(2026, 5, 1, 18, 5, 0), new DateTime(2026, 5, 1, 18, 5, 0) };
        var different = new[] { new DateTime(2026, 5, 1, 18, 5, 0), new DateTime(2026, 5, 1, 9, 30, 0) };

        Assert.True(TransactionRowPresentation.AllShareSameTime(same));
        Assert.False(TransactionRowPresentation.AllShareSameTime(different));
        Assert.False(TransactionRowPresentation.AllShareSameTime(same.Take(1)));
    }

    [Theory]
    [InlineData("Salary", 100.0)]
    [InlineData("GROCERIES", -20.0)]
    public void IconFor_MapsKnownLabelsCaseInsensitively(string label, double amount)
    {
        var icon = TransactionRowPresentation.IconFor(label, (decimal)amount);

        Assert.NotEqual(Icons.Material.Filled.ArrowDownward, icon);
        Assert.NotEqual(Icons.Material.Filled.ArrowUpward, icon);
    }

    [Theory]
    [InlineData(null, 10.0)]
    [InlineData("Something unusual", 10.0)]
    public void IconFor_FallsBackToDirectionOfMoneyFlow(string? label, double amount)
    {
        Assert.Equal(Icons.Material.Filled.ArrowDownward, TransactionRowPresentation.IconFor(label, (decimal)amount));
        Assert.Equal(Icons.Material.Filled.ArrowUpward, TransactionRowPresentation.IconFor(label, -(decimal)amount));
    }
}