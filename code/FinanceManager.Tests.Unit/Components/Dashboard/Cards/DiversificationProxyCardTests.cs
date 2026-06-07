using FinanceManager.Components.Components.Features.Dashboard.Cards;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Tests.Unit.Components.Dashboard.Cards;

[Trait("Category", "Unit")]
public class DiversificationProxyCardTests
{
    [Fact]
    public void BuildExplanations_ModerateBand_ReturnsEmpty()
    {
        var score = new DiversificationScore(50, 25, 25, "Moderate");

        var result = DiversificationProxyCard.BuildExplanations(score);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildExplanations_LimitedBand_LowAssetClassAndLowHoldings_ReturnsBoth()
    {
        var score = new DiversificationScore(20, 10, 10, "Limited");

        var result = DiversificationProxyCard.BuildExplanations(score);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Contains("asset-class spread", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, r => r.Contains("unique holdings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildExplanations_LimitedBand_OnlyAssetClassLow_ReturnsOnlyAssetClassBullet()
    {
        var score = new DiversificationScore(33, 8, 25, "Limited");

        var result = DiversificationProxyCard.BuildExplanations(score);

        var bullet = Assert.Single(result);
        Assert.Contains("asset-class spread", bullet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExplanations_LimitedBand_NoSubScoreLow_ReturnsEmpty()
    {
        var score = new DiversificationScore(33, 20, 13, "Limited");

        var result = DiversificationProxyCard.BuildExplanations(score);

        // AssetClassScore 20 is above the low threshold (16); only HoldingsScore 13 triggers a bullet.
        var bullet = Assert.Single(result);
        Assert.Contains("unique holdings", bullet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExplanations_BroadBand_HighAssetClassAndHighHoldings_ReturnsBoth()
    {
        var score = new DiversificationScore(85, 42, 43, "Broad");

        var result = DiversificationProxyCard.BuildExplanations(score);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Contains("Broad asset-class spread", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, r => r.Contains("Many unique holdings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildExplanations_BroadBand_OnlyHoldingsHigh_ReturnsOnlyHoldingsBullet()
    {
        var score = new DiversificationScore(67, 25, 42, "Broad");

        var result = DiversificationProxyCard.BuildExplanations(score);

        var bullet = Assert.Single(result);
        Assert.Contains("Many unique holdings", bullet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExplanations_LimitedBand_AtLowThresholdBoundary_TriggersBullet()
    {
        var score = new DiversificationScore(32, 16, 16, "Limited");

        var result = DiversificationProxyCard.BuildExplanations(score);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void BuildExplanations_BroadBand_AtHighThresholdBoundary_TriggersBullet()
    {
        var score = new DiversificationScore(68, 34, 34, "Broad");

        var result = DiversificationProxyCard.BuildExplanations(score);

        Assert.Equal(2, result.Count);
    }
}