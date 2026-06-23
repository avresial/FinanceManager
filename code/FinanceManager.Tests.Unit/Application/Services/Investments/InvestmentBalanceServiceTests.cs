using FinanceManager.Application.FinancialAccounts.Investments.Balance;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class InvestmentBalanceServiceTests
{
    private const int _userId = 1;

    private readonly Mock<IFinancialAccountRepository> _financialAccountRepository = new();
    private readonly Mock<IInvestmentValuationService> _valuationService = new();
    private readonly InvestmentBalanceService _service;

    public InvestmentBalanceServiceTests()
    {
        _service = new InvestmentBalanceService(_financialAccountRepository.Object, _valuationService.Object);
    }

    [Fact]
    public void IsOfType_StockAccount_IsTrue()
    {
        Assert.True(_service.IsOfType<StockAccount>());
        Assert.False(_service.IsOfType<CurrencyAccount>());
    }

    [Fact]
    public async Task GetClosingBalance_AggregatesValuationSeriesAcrossInvestmentAccounts()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 3);

        var account1 = new StockAccount(_userId, 10, "Investments A");
        var account2 = new StockAccount(_userId, 20, "Investments B");

        _financialAccountRepository.Setup(repo => repo.GetAccounts<StockAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                   .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        _valuationService.Setup(x => x.GetAccountValueSeriesAsync(10, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal>
                         {
                             [start] = 100,
                             [start.AddDays(1)] = 110,
                             [end] = 120
                         });
        _valuationService.Setup(x => x.GetAccountValueSeriesAsync(20, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal>
                         {
                             [start] = 5,
                             [end] = 7
                         });

        var result = await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end);

        Assert.Equal(3, result.Count);
        Assert.Equal(105, result.Single(x => x.DateTime == start).Value);
        // account2 omitted the middle day (valued to zero), so only account1 contributes there.
        Assert.Equal(110, result.Single(x => x.DateTime == start.AddDays(1)).Value);
        Assert.Equal(127, result.Single(x => x.DateTime == end).Value);
    }

    [Fact]
    public async Task GetClosingBalance_RespectsAccountIdFilter()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 1);

        var account1 = new StockAccount(_userId, 10, "Investments A");
        var account2 = new StockAccount(_userId, 20, "Investments B");

        _financialAccountRepository.Setup(repo => repo.GetAccounts<StockAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                   .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        _valuationService.Setup(x => x.GetAccountValueSeriesAsync(10, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 100 });

        var result = await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end, new[] { 10 });

        Assert.Single(result);
        Assert.Equal(100, result.Single().Value);
        _valuationService.Verify(x => x.GetAccountValueSeriesAsync(20, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetClosingBalance_LegacyStockAccountWithEmptySeries_ContributesNothing()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 2);

        var legacyStockAccount = new StockAccount(_userId, 30, "Legacy stocks");

        _financialAccountRepository.Setup(repo => repo.GetAccounts<StockAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                   .Returns(new[] { legacyStockAccount }.ToAsyncEnumerable());

        // Legacy stock accounts hold no investment transactions, so the valuation service returns empty.
        _valuationService.Setup(x => x.GetAccountValueSeriesAsync(30, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal>());

        var result = await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end);

        Assert.All(result, point => Assert.Equal(0, point.Value));
    }

    [Fact]
    public async Task CashFlow_ReturnsEmpty_ToAvoidDoubleCountingAgainstCashAccount()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 31);

        Assert.Empty(await _service.GetInflow(_userId, DefaultCurrency.PLN, start, end));
        Assert.Empty(await _service.GetOutflow(_userId, DefaultCurrency.PLN, start, end));
        Assert.Empty(await _service.GetNetCashFlow(_userId, DefaultCurrency.PLN, start, end));
        Assert.Empty(await _service.GetInflow(_userId, DefaultCurrency.PLN, start, end, new[] { 10 }));
        Assert.Empty(await _service.GetOutflow(_userId, DefaultCurrency.PLN, start, end, new[] { 10 }));
        Assert.Empty(await _service.GetNetCashFlow(_userId, DefaultCurrency.PLN, start, end, new[] { 10 }));

        // No account enumeration or valuation should happen for cash-flow paths.
        _financialAccountRepository.Verify(repo => repo.GetAccounts<StockAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        _valuationService.VerifyNoOtherCalls();
    }
}