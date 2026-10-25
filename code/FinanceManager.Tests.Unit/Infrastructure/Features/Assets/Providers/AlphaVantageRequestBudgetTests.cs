using FinanceManager.Infrastructure.Features.Assets.Providers;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Trait("Category", "Unit")]
public class AlphaVantageRequestBudgetTests
{
    [Fact]
    public void TryConsume_StopsAtFreeTierDailyLimit()
    {
        var budget = new AlphaVantageRequestBudget();
        Assert.All(Enumerable.Range(0, 25), _ => Assert.True(budget.TryConsume()));
        Assert.False(budget.TryConsume());
        Assert.True(budget.TryConsume(DateTimeOffset.UtcNow.AddDays(1)));
    }
}