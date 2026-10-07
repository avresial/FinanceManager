using FinanceManager.Components.Features.Dashboard.Models;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Models;

[Trait("Category", "Unit")]
public class DashboardCardLayoutTests
{
    private static readonly DashboardCardSpan _narrowThird = new(4, 6);
    private static readonly DashboardCardSpan _narrowHalf = new(6, 6);
    private static readonly DashboardCardSpan _wideHalf = new(6, 12);

    private static (int Span, int Filler)[] Arrange(params DashboardCardSpan[] cards) =>
        [.. DashboardCardLayout.Arrange(cards).Select(p => (p.Span, p.FillerSpanAfter))];

    [Fact]
    public void NoCards_ReturnsEmpty() =>
        Assert.Empty(DashboardCardLayout.Arrange([]));

    [Fact]
    public void FullRows_KeepPreferredWidth() =>
        Assert.Equal([(4, 0), (4, 0), (4, 0), (4, 0), (4, 0), (4, 0)], Arrange([.. Enumerable.Repeat(_narrowThird, 6)]));

    [Fact]
    public void FourThirds_BalanceIntoTwoRowsOfHalves() =>
        Assert.Equal([(6, 0), (6, 0), (6, 0), (6, 0)], Arrange([.. Enumerable.Repeat(_narrowThird, 4)]));

    [Fact]
    public void FiveThirds_ShortRowWidensToHalves() =>
        Assert.Equal([(4, 0), (4, 0), (4, 0), (6, 0), (6, 0)], Arrange([.. Enumerable.Repeat(_narrowThird, 5)]));

    [Fact]
    public void SingleNarrowCard_StopsAtMaxAndLeavesFiller() =>
        Assert.Equal([(6, 6)], Arrange(_narrowThird));

    [Fact]
    public void LeftoverWideCard_FillsRow() =>
        Assert.Equal([(6, 0), (6, 0), (12, 0)], Arrange(_wideHalf, _narrowHalf, _wideHalf));

    [Fact]
    public void LeftoverNarrowCard_IsNotStretched() =>
        Assert.Equal([(6, 0), (6, 0), (6, 6)], Arrange(_wideHalf, _wideHalf, _narrowHalf));

    [Fact]
    public void PreferredWiderThanMax_Throws() =>
        Assert.Throws<ArgumentException>(() => DashboardCardLayout.Arrange([new DashboardCardSpan(6, 4)]));

    [Fact]
    public void FillingOrder_NoFiller_KeepsOrder() =>
        Assert.Equal([0, 1, 2, 3], DashboardCardLayout.FillingOrder([_wideHalf, _wideHalf, _wideHalf, _narrowHalf]));

    [Fact]
    public void FillingOrder_NarrowCardLeftOver_MovesNearestWideCardToEnd() =>
        Assert.Equal([0, 2, 1], DashboardCardLayout.FillingOrder([_wideHalf, _wideHalf, _narrowHalf]));

    [Fact]
    public void FillingOrder_NoCardCanWiden_KeepsOrder() =>
        Assert.Equal([0, 1, 2], DashboardCardLayout.FillingOrder([_narrowHalf, _narrowHalf, _narrowHalf]));

    [Fact]
    public void FillingOrder_NoCards_ReturnsEmpty() =>
        Assert.Empty(DashboardCardLayout.FillingOrder([]));
}