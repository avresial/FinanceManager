using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Runtime.CompilerServices;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class InvestmentRateCardTests
{
    [Fact]
    public void SelectMonth_UpdatesSelectionAndIgnoresPlaceholderBars()
    {
        var january = new InvestmentRate { Start = new DateTime(2026, 1, 1) };
        var february = new InvestmentRate { Start = new DateTime(2026, 2, 1) };
        var card = (InvestmentRateCard)RuntimeHelpers.GetUninitializedObject(typeof(InvestmentRateCard));
        card.MonthlyInvestmentRates = [january, february];

        Assert.True(card.SelectMonth(0));
        Assert.Same(january, card.SelectedMonthRate);
        Assert.False(card.SelectMonth(2));
        Assert.Same(january, card.SelectedMonthRate);
    }
}