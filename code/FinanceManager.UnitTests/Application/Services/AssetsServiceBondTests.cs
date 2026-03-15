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
public class AssetsServiceBondTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly AssetsServiceBond _service;

    public AssetsServiceBondTests()
    {
        _service = new AssetsServiceBond(_financialAccountRepositoryMock.Object, _bondDetailsRepositoryMock.Object);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ShouldIncludeCarriedForwardBondHoldings()
    {
        var startDate = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddDays(2);

        var account = new BondAccount(
            1,
            1,
            "Bonds",
            [new BondAccountEntry(1, 2, startDate, 5m, 5m, 2)],
            AccountLabel.Other,
            new Dictionary<int, BondAccountEntry>
            {
                [1] = new BondAccountEntry(1, 1, startDate.AddDays(-5), 10m, 10m, 1)
            });

        var bondDetails = new[]
        {
            new BondDetails(
                "Bond A",
                "Issuer A",
                DateOnly.FromDateTime(startDate.AddYears(-1)),
                DateOnly.FromDateTime(endDate.AddYears(1)),
                [new BondCalculationMethod { Id = 1, DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
                DefaultCurrency.PLN,
                BondType.InflationBond,
                1m)
            { Id = 1 },
            new BondDetails(
                "Bond B",
                "Issuer B",
                DateOnly.FromDateTime(startDate),
                DateOnly.FromDateTime(endDate.AddYears(1)),
                [new BondCalculationMethod { Id = 2, DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
                DefaultCurrency.PLN,
                BondType.InflationBond,
                1m)
            { Id = 2 }
        };

        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(bondDetails.ToAsyncEnumerable());

        var result = await _service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(3, result.Count);
        Assert.All(result, point => Assert.Equal(15m, point.Value));
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_ShouldIncludeCarriedForwardBondHoldings()
    {
        var asOfDate = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        var account = new BondAccount(
            1,
            1,
            "Bonds",
            [new BondAccountEntry(1, 2, asOfDate.Date.AddHours(8), 5m, 5m, 2)],
            AccountLabel.Other,
            new Dictionary<int, BondAccountEntry>
            {
                [1] = new BondAccountEntry(1, 1, asOfDate.AddDays(-5), 10m, 10m, 1)
            });

        var bondDetails = new[]
        {
            new BondDetails(
                "Bond A",
                "Issuer A",
                DateOnly.FromDateTime(asOfDate.AddYears(-1)),
                DateOnly.FromDateTime(asOfDate.AddYears(1)),
                [new BondCalculationMethod { Id = 1, DateOperator = DateOperator.UntilDate, DateValue = asOfDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
                DefaultCurrency.PLN,
                BondType.InflationBond,
                1m)
            { Id = 1 },
            new BondDetails(
                "Bond B",
                "Issuer B",
                DateOnly.FromDateTime(asOfDate),
                DateOnly.FromDateTime(asOfDate.AddYears(1)),
                [new BondCalculationMethod { Id = 2, DateOperator = DateOperator.UntilDate, DateValue = asOfDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
                DefaultCurrency.PLN,
                BondType.InflationBond,
                1m)
            { Id = 2 }
        };

        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(bondDetails.ToAsyncEnumerable());

        var result = await _service.GetEndAssetsPerAccount(1, DefaultCurrency.PLN, asOfDate)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal(15m, result[0].Value);
    }
}