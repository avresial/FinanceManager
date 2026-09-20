using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class RecurringTransactionDetectorCardViewTests
{
    [Fact]
    public async Task RecurringIncome_IsShownAsAPositiveSuccessAmount()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        var cut = context.Render<RecurringTransactionDetectorCardView>(parameters => parameters
            .Add(x => x.Currency, "PLN")
            .Add(x => x.Data,
            [
                new RecurringTransactionResult("Monthly salary", 5_000m) { IsIncome = true },
                new RecurringTransactionResult("Rent", 1_000m)
            ]));

        Assert.Contains("Monthly salary", cut.Markup);
        var incomeAmount = cut.FindAll(".mud-typography")
            .Single(element => Regex.IsMatch(element.TextContent, @"\+5000[.,]00 PLN/mo"));
        Assert.Contains("mud-success-text", incomeAmount.ClassList);
    }
}