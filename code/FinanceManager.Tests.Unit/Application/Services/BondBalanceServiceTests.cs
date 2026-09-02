using FinanceManager.Application.FinancialAccounts.Bond.Balance;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BondBalanceServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeServiceMock = new();
    private readonly BondBalanceService _service;

    public BondBalanceServiceTests()
    {
        _service = new BondBalanceService(
            _financialAccountRepositoryMock.Object,
            _bondDetailsRepositoryMock.Object,
            _currencyExchangeServiceMock.Object);
    }

    [Fact]
    public async Task GetClosingBalance_ReturnsDailyBondValue()
    {
        var userId = 1;
        DateTime startDate = new(2024, 1, 1);
        DateTime endDate = new(2024, 1, 3);
        var account = new BondAccount(userId, 1, "Bonds",
        [
            new BondAccountEntry(1, 1, startDate, 100, 100, 1)
        ], AccountLabel.Other);

        var details = new BondDetails(
            "Bond",
            "Issuer",
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate.AddYears(1)),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0 }],
            DefaultCurrency.PLN,
            BondType.InflationBond,
            100m)
        {
            Id = 1
        };

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(new[] { details }.ToAsyncEnumerable());

        var result = await _service.GetClosingBalance(userId, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(3, result.Count);
        Assert.All(result, point => Assert.Equal(10000, point.Value));
    }

    [Fact]
    public async Task GetNetCashFlow_ReturnsSignedTransactionValueSeries()
    {
        var userId = 1;
        DateTime startDate = new(2024, 1, 1);
        DateTime endDate = new(2024, 1, 2);
        var account = new BondAccount(userId, 1, "Bonds",
        [
            new BondAccountEntry(1, 2, endDate, 50, -50, 1),
            new BondAccountEntry(1, 1, startDate, 100, 100, 1)
        ], AccountLabel.Other);

        var details = new BondDetails(
            "Bond",
            "Issuer",
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate.AddYears(1)),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0 }],
            DefaultCurrency.PLN,
            BondType.InflationBond,
            100m)
        {
            Id = 1
        };

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(new[] { details }.ToAsyncEnumerable());

        var result = await _service.GetNetCashFlow(userId, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(2, result.Count);
        Assert.Equal(10000, result.Single(x => x.DateTime == startDate).Value);
        Assert.Equal(-5000, result.Single(x => x.DateTime == endDate).Value);
    }

    [Fact]
    public async Task GetCapital_CarriesOpeningUnits_CombinesSameDayEntries_AndExcludesInterest()
    {
        var userId = 1;
        DateTime startDate = new(2024, 1, 2);
        DateTime endDate = new(2024, 1, 4);
        var account = new BondAccount(userId, 1, "Bonds",
        [
            new BondAccountEntry(1, 1, new DateTime(2024, 1, 1), 10, 10, 1),
            new BondAccountEntry(1, 2, startDate, 12, 2, 1),
            new BondAccountEntry(1, 3, startDate, 13, 1, 1),
            new BondAccountEntry(1, 4, new DateTime(2024, 1, 3), 8, -5, 1)
        ], AccountLabel.Other);

        var details = CreateDetails(startDate, endDate, DefaultCurrency.PLN);
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(new[] { details }.ToAsyncEnumerable());

        var result = await _service.GetCapital(userId, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(1_300m, result.Single(x => x.DateTime == startDate).Value);
        Assert.Equal(800m, result.Single(x => x.DateTime == startDate.AddDays(1)).Value);
        Assert.Equal(800m, result.Single(x => x.DateTime == endDate).Value);
    }

    [Fact]
    public async Task GetCapital_ConvertsBondCashFlowsUsingHistoricalRates()
    {
        var userId = 1;
        DateTime startDate = new(2024, 1, 2);
        DateTime endDate = new(2024, 1, 3);
        var account = new BondAccount(userId, 1, "Bonds",
        [
            new BondAccountEntry(1, 1, startDate, 10, 10, 1),
            new BondAccountEntry(1, 2, endDate, 8, -2, 1)
        ], AccountLabel.Other);
        var details = CreateDetails(startDate, endDate, DefaultCurrency.USD);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).Returns(new[] { details }.ToAsyncEnumerable());
        _currencyExchangeServiceMock
            .Setup(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, startDate, endDate))
            .ReturnsAsync([
                (startDate, (decimal?)2m),
                (endDate, (decimal?)3m)
            ]);

        var result = await _service.GetCapital(userId, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(2_000m, result.Single(x => x.DateTime == startDate).Value);
        Assert.Equal(1_400m, result.Single(x => x.DateTime == endDate).Value);
        _currencyExchangeServiceMock.Verify(
            x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, startDate, endDate),
            Times.Once);
    }

    private static BondDetails CreateDetails(DateTime startDate, DateTime endDate, Currency currency) =>
        new(
            "Bond",
            "Issuer",
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate.AddYears(1)),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0 }],
            currency,
            BondType.InflationBond,
            100m)
        {
            Id = 1
        };
}