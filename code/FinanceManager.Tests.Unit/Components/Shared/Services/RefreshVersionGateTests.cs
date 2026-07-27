using FinanceManager.Components.Shared.Services;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class RefreshVersionGateTests
{
    [Fact]
    public void Claim_OnlyTheLatestClaimIsCurrent()
    {
        var gate = new RefreshVersionGate();

        var first = gate.Claim();
        var second = gate.Claim();

        Assert.False(gate.IsCurrent(first));
        Assert.True(gate.IsCurrent(second));
    }

    [Fact]
    public void IsCurrent_SingleClaim_StaysCurrent()
    {
        var gate = new RefreshVersionGate();

        Assert.True(gate.IsCurrent(gate.Claim()));
    }
}