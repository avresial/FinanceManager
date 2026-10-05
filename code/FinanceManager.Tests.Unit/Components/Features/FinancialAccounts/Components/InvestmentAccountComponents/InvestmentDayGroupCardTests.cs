using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents.TransactionHistory;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

[Trait("Category", "Unit")]
public class InvestmentDayGroupCardTests
{
    [Fact]
    public async Task Header_SumsCashImpact_EvenForPricedBuys()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var buy = new InvestmentTransactionDto(1, 1, 1, 1, InvestmentTransactionType.Buy, 2m, 100m, "USD",
            new DateOnly(2026, 10, 1), 1m, null, "AAPL");
        var valuation = new InvestmentTransactionValuationDto(1, 400m, 800m, 150m, 1200m, 400m, 50m, "PLN", true, true);

        var cut = context.Render<InvestmentDayGroupCard>(p => p
            .Add(x => x.Day, buy.TradeDate)
            .Add(x => x.Transactions, [buy])
            .Add(x => x.Currency, "PLN")
            .Add(x => x.Valuations, new Dictionary<long, InvestmentTransactionValuationDto> { [1] = valuation }));

        var header = cut.Find(".fm-card-info-actions").TextContent;
        Assert.Contains("-201.00 USD", header);
        Assert.DoesNotContain("400.00", header);
    }
}