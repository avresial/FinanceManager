using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public sealed class InvestmentPaycheckEstimatorViewTests
{
    [Theory]
    [InlineData(120_000, 0.04, 400)]
    [InlineData(240_000, 0.03, 600)]
    [InlineData(100_000, 0.05, 416.67)]
    public async Task MonthlyPaycheck_ComputesLocally(decimal assets, decimal rate, decimal expected)
    {
        await using var context = CreateContext();
        var cut = context.Render<InvestmentPaycheckEstimatorView>(parameters => parameters
            .Add(view => view.Source, Source(assets))
            .Add(view => view.Currency, DefaultCurrency.PLN));
        cut.Instance.OnRateChanged(rate);

        Assert.Equal(expected, cut.Instance.MonthlyPaycheck);
    }

    [Fact]
    public async Task ReplacementRatio_TracksLocalRateWithoutResettingItWhenSourceChanges()
    {
        await using var context = CreateContext();
        var cut = context.Render<InvestmentPaycheckEstimatorView>(parameters => parameters
            .Add(view => view.Source, Source(120_000m, averageSalary: 800m))
            .Add(view => view.Currency, DefaultCurrency.PLN));
        cut.Instance.OnPresetSelected(0.08m);
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvestmentPaycheckEstimatorView.Source)] = Source(240_000m, averageSalary: 800m),
            })));

        Assert.Equal(0.08m, cut.Instance._annualWithdrawalRate);
        Assert.Equal(1_600m, cut.Instance.MonthlyPaycheck);
        Assert.Equal(2m, cut.Instance.ReplacementRatio);
        Assert.Contains("Aggressive", cut.Markup);
    }

    [Fact]
    public async Task EmptySalarySource_IsARealZeroEstimateAndUnavailableRatiosStayHidden()
    {
        await using var context = CreateContext();
        var cut = context.Render<InvestmentPaycheckEstimatorView>(parameters => parameters
            .Add(view => view.Source, Source(0m))
            .Add(view => view.Currency, DefaultCurrency.PLN));

        Assert.Equal(0m, cut.Instance.MonthlyPaycheck);
        Assert.Null(cut.Instance.ReplacementRatio);
        Assert.Contains("No salary history yet", cut.Markup);
        Assert.Contains("0.00 PLN", cut.Markup);
    }

    private static InvestmentPaycheckSourceModel Source(decimal assets, decimal? averageSalary = null) => new(
        assets,
        3,
        averageSalary.HasValue ? 3 : 0,
        averageSalary);

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        return context;
    }
}