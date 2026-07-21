using FinanceManager.Application.FinancialAccounts.Investments.Balance;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
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
    public async Task GetClosingBalance_AggregatesValuationSeriesAcrossInvestmentAccounts()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 3);

        var account1 = new InvestmentAccount(_userId, 10, "Investments A");
        var account2 = new InvestmentAccount(_userId, 20, "Investments B");

        _financialAccountRepository.Setup(repo => repo.GetAccounts<InvestmentAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                   .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        _valuationService.Setup(x => x.GetAccountValueSeriesAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(10) && a.Contains(20)), DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>
                         {
                             [10] = new Dictionary<DateTime, decimal>
                             {
                                 [start] = 100,
                                 [start.AddDays(1)] = 110,
                                 [end] = 120
                             },
                             [20] = new Dictionary<DateTime, decimal>
                             {
                                 [start] = 5,
                                 [end] = 7
                             }
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

        var account1 = new InvestmentAccount(_userId, 10, "Investments A");
        var account2 = new InvestmentAccount(_userId, 20, "Investments B");

        _financialAccountRepository.Setup(repo => repo.GetAccounts<InvestmentAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                   .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        _valuationService.Setup(x => x.GetAccountValueSeriesAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(10)), DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>
                         {
                             [10] = new Dictionary<DateTime, decimal> { [start] = 100 }
                         });

        var result = await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end, new[] { 10 });

        Assert.Single(result);
        Assert.Equal(100, result.Single().Value);
        // The filtered-out account 20 must never reach the batched valuation call.
        _valuationService.Verify(x => x.GetAccountValueSeriesAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(20)), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetClosingBalance_LegacyStockAccountWithEmptySeries_ContributesNothing()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 2);

        var legacyStockAccount = new InvestmentAccount(_userId, 30, "Legacy stocks");

        _financialAccountRepository.Setup(repo => repo.GetAccounts<InvestmentAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                   .Returns(new[] { legacyStockAccount }.ToAsyncEnumerable());

        // Legacy stock accounts hold no investment transactions, so the batched valuation result
        // simply omits them (empty dictionary).
        _valuationService.Setup(x => x.GetAccountValueSeriesAsync(It.IsAny<IReadOnlyCollection<int>>(), DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>());

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
        _financialAccountRepository.Verify(repo => repo.GetAccounts<InvestmentAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        _valuationService.VerifyNoOtherCalls();
    }
}