using FinanceManager.Application.FinancialAccounts.Investments.Assets;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AssetsServiceInvestmentTests
{
    [Fact]
    public async Task GetUnrealizedGainLossPerAccount_ConvertsRemainingBuyCostAtTradeDate()
    {
        var accountRepository = new Mock<IFinancialAccountRepository>();
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var priceProvider = new Mock<IInvestmentPriceProvider>();
        var exchangeService = new Mock<ICurrencyExchangeService>();
        var valuationService = new Mock<IInvestmentValuationService>();
        var account = new InvestmentAccount(1, 7, "Broker");
        var tradeDate = new DateOnly(2026, 1, 10);
        var listing = new AssetListing { Id = 11, Ticker = "CSPX" };
        var transactions = new List<InvestmentTransaction>
        {
            new()
            {
                AccountId = account.AccountId,
                AssetListingId = listing.Id,
                AssetListing = listing,
                Type = InvestmentTransactionType.Buy,
                Quantity = 10m,
                UnitPrice = 100m,
                Fee = 20m,
                Currency = "USD",
                TradeDate = tradeDate,
            },
            new()
            {
                AccountId = account.AccountId,
                AssetListingId = listing.Id,
                AssetListing = listing,
                Type = InvestmentTransactionType.Sell,
                Quantity = 5m,
                UnitPrice = 120m,
                Currency = "USD",
                TradeDate = tradeDate.AddDays(1),
            },
        };

        accountRepository
            .Setup(x => x.GetAccounts<InvestmentAccount>(1, DateTime.MinValue, It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());
        transactionRepository.Setup(x => x.GetByAccount(account.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
        priceProvider.Setup(x => x.GetPricePerUnitAsync(listing.Id, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);
        exchangeService.Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == "USD"), DefaultCurrency.PLN,
                tradeDate.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(4m);

        var service = new AssetsServiceInvestment(
            accountRepository.Object,
            valuationService.Object,
            transactionRepository.Object,
            priceProvider.Object,
            exchangeService.Object);

        var result = Assert.Single(await service.GetUnrealizedGainLossPerAccount(1, DefaultCurrency.PLN, DateTime.UtcNow));

        Assert.Equal(2040m, result.CostBasis);
        Assert.Equal(2500m, result.CurrentValue);
        Assert.Equal(460m, result.UnrealizedGainLoss);
    }
}