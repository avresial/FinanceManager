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
        var card = CreateCard(new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc));
        card.MonthlyInvestmentRates = [january, february];

        Assert.True(card.SelectMonth(0));
        Assert.Same(january, card.SelectedMonthRate);
        Assert.False(card.SelectMonth(2));
        Assert.Same(january, card.SelectedMonthRate);
    }

    [Fact]
    public void BuildDerivedState_CurrentMonthWithoutSalary_ReportsNoRate()
    {
        // The salary for the current month has not arrived yet, but investments were made. The card
        // must not show a rate for that month, must not plot a bar for it, and must keep it out of
        // the average — otherwise it reads as if the salary had already been received.
        var june = new InvestmentRate { Start = new DateTime(2026, 6, 1), Salary = 9456.88m, InvestmentsChange = 4000m };
        var july = new InvestmentRate { Start = new DateTime(2026, 7, 1), Salary = 0m, InvestmentsChange = 5523.17m };
        var card = CreateCard(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc));
        card.MonthlyInvestmentRates = [june, july];

        card.BuildDerivedState();

        Assert.Null(card.CurrentMonthPercentage);
        Assert.Equal(4000m / 9456.88m, card.YtdAveragePercentage);
        Assert.Null(card.Series[1].Percentage);
        Assert.Equal(4000m / 9456.88m * 100m, card.Series[0].Percentage);
    }

    [Fact]
    public void BuildDerivedState_CurrentMonthWithSalary_ReportsRate()
    {
        var july = new InvestmentRate { Start = new DateTime(2026, 7, 1), Salary = 10_000m, InvestmentsChange = 5000m };
        var card = CreateCard(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc));
        card.MonthlyInvestmentRates = [july];

        card.BuildDerivedState();

        Assert.Equal(0.5m, card.CurrentMonthPercentage);
        Assert.Equal(0.5m, card.YtdAveragePercentage);
        Assert.Equal(50m, card.Series[0].Percentage);
    }

    // The component is never rendered here, so field initialisers and injected dependencies are not
    // needed — only the state the derived-value calculations read.
    private static InvestmentRateCard CreateCard(DateTime asOfDate)
    {
        var card = (InvestmentRateCard)RuntimeHelpers.GetUninitializedObject(typeof(InvestmentRateCard));
        card.AsOfDate = asOfDate;
        return card;
    }
}