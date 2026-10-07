using FinanceManager.Components.Features.Dashboard.Models;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Models;

[Trait("Category", "Unit")]
public class DashboardCardLayoutTests
{
    private static readonly DashboardCardSpan _narrow = new(4, 6);
    private static readonly DashboardCardSpan _wide = new(6, 12);
    private static readonly DashboardCardSpan _full = new(12, 12);

    private static (int Span, int Filler)[] Arrange(params DashboardCardSpan[] cards) =>
        [.. DashboardCardLayout.Arrange(cards).Select(p => (p.Span, p.FillerSpanAfter))];

    [Fact]
    public void NoCards_ReturnsEmpty() =>
        Assert.Empty(DashboardCardLayout.Arrange([]));

    [Fact]
    public void FullRows_KeepPreferredWidth() =>
        Assert.Equal([(4, 0), (4, 0), (4, 0), (4, 0), (4, 0), (4, 0)], Arrange(_narrow, _narrow, _narrow, _narrow, _narrow, _narrow));

    [Fact]
    public void FourNarrow_BalanceIntoTwoRowsOfHalves() =>
        Assert.Equal([(6, 0), (6, 0), (6, 0), (6, 0)], Arrange(_narrow, _narrow, _narrow, _narrow));

    [Fact]
    public void SevenNarrow_FillEveryRow() =>
        Assert.Equal([(4, 0), (4, 0), (4, 0), (6, 0), (6, 0), (6, 0), (6, 0)],
            Arrange(_narrow, _narrow, _narrow, _narrow, _narrow, _narrow, _narrow));

    [Fact]
    public void WideAndNarrow_ShareRow_WideCardTakesSpareColumns() =>
        Assert.Equal([(8, 0), (4, 0)], Arrange(_wide, _narrow));

    [Fact]
    public void WideCards_PairUp() =>
        Assert.Equal([(6, 0), (6, 0)], Arrange(_wide, _wide));

    [Fact]
    public void FullCards_TakeTheirOwnRows() =>
        Assert.Equal([(12, 0), (8, 0), (4, 0), (12, 0)], Arrange(_full, _wide, _narrow, _full));

    [Fact]
    public void DefaultDashboard_FillsEveryRowWithoutFiller() =>
        Assert.All(DashboardCardLayout.Arrange([_full, _wide, _narrow, _wide, .. Enumerable.Repeat(_narrow, 7), _full]),
            placement => Assert.Equal(0, placement.FillerSpanAfter));

    [Fact]
    public void LoneNarrowCard_StopsAtMaxAndLeavesFiller() =>
        Assert.Equal([(12, 0), (6, 6), (12, 0)], Arrange(_full, _narrow, _full));

    [Fact]
    public void LoneWideCard_FillsRow() =>
        Assert.Equal([(12, 0)], Arrange(_wide));

    [Fact]
    public void PreferredWiderThanMax_Throws() =>
        Assert.Throws<ArgumentException>(() => DashboardCardLayout.Arrange([new DashboardCardSpan(6, 4)]));
}