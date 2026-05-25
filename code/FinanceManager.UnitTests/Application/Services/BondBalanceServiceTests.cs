using FinanceManager.Application.Services.Bonds;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BondBalanceServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly BondBalanceService _service;

    public BondBalanceServiceTests()
    {
        _service = new BondBalanceService(_financialAccountRepositoryMock.Object, _bondDetailsRepositoryMock.Object);
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
}