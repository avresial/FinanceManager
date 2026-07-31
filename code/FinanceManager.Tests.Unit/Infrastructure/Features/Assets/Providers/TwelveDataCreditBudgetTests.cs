using FinanceManager.Application.Shared.Options;
using FinanceManager.Infrastructure.Features.Assets.Providers;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Assets.Providers;

[Trait("Category", "Unit")]
public class TwelveDataCreditBudgetTests
{
    [Fact]
    public void EnforcesMinuteAndDailyLimitsAndResetsAtUtcBoundaries()
    {
        var budget = new TwelveDataCreditBudget(Options.Create(new TwelveDataOptions
        {
            PerMinuteCreditLimit = 2,
            DailyCreditLimit = 3
        }));
        var now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.True(budget.TryConsume(now));
        Assert.True(budget.TryConsume(now));
        Assert.False(budget.TryConsume(now));
        Assert.True(budget.TryConsume(now.AddMinutes(1)));
        Assert.False(budget.TryConsume(now.AddMinutes(2)));
        Assert.True(budget.TryConsume(now.AddDays(1)));
    }
}