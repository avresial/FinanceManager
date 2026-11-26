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

    private readonly Mock<IAccountRepository<InvestmentAccount>> _investmentAccountRepository = new();
    private readonly Mock<IInvestmentValuationService> _valuationService = new();
    private readonly Mock<IInvestmentCapitalSeriesService> _capitalSeriesService = new();
    private readonly InvestmentBalanceService _service;

    public InvestmentBalanceServiceTests()
    {
        _service = new InvestmentBalanceService(_investmentAccountRepository.Object, _valuationService.Object, _capitalSeriesService.Object);
    }

    [Fact]
    public async Task GetClosingBalance_Unfiltered_UsesThePooledOwnerSeriesAndZeroFillsMissingDays()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 3);

        // The pooled series omits the middle day (valued to zero); the closing balance still reports that day.
        _valuationService.Setup(x => x.GetUserValueSeriesAsync(_userId, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 105, [end] = 127 });

        var result = await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end);

        Assert.Equal([105m, 0m, 127m], result.Select(x => x.Value));
        // Account lookups are only needed for account-filtered charts.
        _investmentAccountRepository.Verify(repo => repo.GetAll(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetCapital_Unfiltered_UsesThePooledOwnerCapitalSeries()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 3);

        _capitalSeriesService.Setup(x => x.GetCapitalSeriesAsync(_userId, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 125, [start.AddDays(1)] = 130, [end] = 180 });

        var result = await _service.GetCapital(_userId, DefaultCurrency.PLN, start, end);

        Assert.Equal(125, result.Single(x => x.DateTime == start).Value);
        Assert.Equal(130, result.Single(x => x.DateTime == start.AddDays(1)).Value);
        Assert.Equal(180, result.Single(x => x.DateTime == end).Value);
        _investmentAccountRepository.Verify(repo => repo.GetAll(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetCapital_AndClosingBalance_ForAGuestUserWithNegativeId_ReadThePooledSeries()
    {
        DateTime start = new(2024, 1, 1);
        _valuationService.Setup(x => x.GetUserValueSeriesAsync(-7, DefaultCurrency.PLN, start, start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 10 });
        _capitalSeriesService.Setup(x => x.GetCapitalSeriesAsync(-7, DefaultCurrency.PLN, start, start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 8 });

        Assert.Equal(10, Assert.Single(await _service.GetClosingBalance(-7, DefaultCurrency.PLN, start, start)).Value);
        Assert.Equal(8, Assert.Single(await _service.GetCapital(-7, DefaultCurrency.PLN, start, start)).Value);
        Assert.Empty(await _service.GetClosingBalance(0, DefaultCurrency.PLN, start, start));
        Assert.Empty(await _service.GetCapital(0, DefaultCurrency.PLN, start, start));
    }

    [Fact]
    public async Task GetClosingBalance_WithTheIdOfAnOwnedInvestmentAccount_ReturnsThePooledOwnerSeries()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 1);
        _investmentAccountRepository.Setup(repo => repo.GetAll(_userId))
            .ReturnsAsync([new InvestmentAccount(_userId, 10, "Investments A"), new InvestmentAccount(_userId, 20, "Investments B")]);
        _valuationService.Setup(x => x.GetUserValueSeriesAsync(_userId, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 100 });

        var result = await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end, new[] { 10 });

        // The pooled series covers every account of the owner (and unlinked trades) until the account screens are removed.
        Assert.Equal(100, Assert.Single(result).Value);
    }

    [Fact]
    public async Task AccountFilteredReads_ForACashOrBondAccountId_ReturnNothing()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 2);
        _investmentAccountRepository.Setup(repo => repo.GetAll(_userId)).ReturnsAsync([new InvestmentAccount(_userId, 10, "Investments A")]);

        Assert.Empty(await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end, new[] { 99 }));
        Assert.Empty(await _service.GetCapital(_userId, DefaultCurrency.PLN, start, end, new[] { 99 }));
        _valuationService.VerifyNoOtherCalls();
        _capitalSeriesService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AccountFilteredReads_IgnoreAnotherUsersInvestmentAccountId()
    {
        DateTime start = new(2024, 1, 1);
        _investmentAccountRepository.Setup(repo => repo.GetAll(_userId)).ReturnsAsync([new InvestmentAccount(999, 10, "Foreign")]);

        Assert.Empty(await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, start, new[] { 10 }));
        _valuationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetClosingBalance_Unfiltered_IsEmpty_WhenTheOwnerHoldsNothing()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 2);
        _valuationService.Setup(x => x.GetUserValueSeriesAsync(_userId, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal>());

        Assert.Empty(await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end));
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
        _investmentAccountRepository.Verify(repo => repo.GetAll(It.IsAny<int>()), Times.Never);
        _valuationService.VerifyNoOtherCalls();
    }
}