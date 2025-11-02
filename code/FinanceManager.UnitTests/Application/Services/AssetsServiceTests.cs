using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class AssetsServiceTests
{
    private readonly Mock<IAssetsServiceTyped> _mockService1 = new Mock<IAssetsServiceTyped>();
    private readonly Mock<IAssetsServiceTyped> _mockService2 = new Mock<IAssetsServiceTyped>();
    private readonly AssetsService _assetsService;

    public AssetsServiceTests()
    {
        // Initialize default behaviors for mocks
        SetupDefaultMockBehaviors(_mockService1);
        SetupDefaultMockBehaviors(_mockService2);

        _assetsService = new([_mockService1.Object, _mockService2.Object]);
    }

    private static void SetupDefaultMockBehaviors(Mock<IAssetsServiceTyped> mock)
    {
        // Default: no assets
        mock.Setup(x => x.IsAnyAccountWithAssets(It.IsAny<int>())).ReturnsAsync(false);

        // Default: empty async enumerable
        async IAsyncEnumerable<NameValueResult> EmptySequence()
        {
            await Task.CompletedTask;
            yield break;
        }

        mock.Setup(x => x.GetEndAssetsPerAccount(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .Returns(() => EmptySequence());

        mock.Setup(x => x.GetEndAssetsPerType(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .Returns(() => EmptySequence());

        // Default: empty list
        mock.Setup(x => x.GetAssetsTimeSeries(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        mock.Setup(x => x.GetAssetsTimeSeries(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<InvestmentType>()))
            .ReturnsAsync([]);
    }
    private static void SetupServiceReturnsPerAccount(Mock<IAssetsServiceTyped> mock, string name, decimal value)
    {
        async IAsyncEnumerable<NameValueResult> Sequence()
        {
            yield return new NameValueResult(name, value);
            await Task.CompletedTask;
        }

        mock.Setup(x => x.GetEndAssetsPerAccount(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .Returns(() => Sequence());
    }

    [Fact]
    public async Task IsAnyAccountWithAssets_ReturnsTrue_WhenAnyTypedServiceHasAssets()
    {
        // Arrange
        _mockService2.Setup(x => x.IsAnyAccountWithAssets(It.IsAny<int>())).ReturnsAsync(true);

        // Act
        var result = await _assetsService.IsAnyAccountWithAssets(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_MergesResultsFromAllTypedServices()
    {
        // Arrange
        SetupServiceReturnsPerAccount(_mockService1, "A", 10);
        SetupServiceReturnsPerAccount(_mockService2, "B", 20);

        // Act
        var list = await _assetsService.GetEndAssetsPerAccount(1, DefaultCurrency.PLN, DateTime.UtcNow)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, list.Count);
        Assert.Contains(list, x => x.Name == "A" && x.Value == 10);
        Assert.Contains(list, x => x.Name == "B" && x.Value == 20);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_AggregatesValuesFromTypedServices()
    {
        // Arrange
        var date = new DateTime(2020, 1, 1);
        _mockService1.Setup(x => x.GetAssetsTimeSeries(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new TimeSeriesModel(date, 10)]);
        _mockService2.Setup(x => x.GetAssetsTimeSeries(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new TimeSeriesModel(date, 5)]);

        // Act
        var aggregated = await _assetsService.GetAssetsTimeSeries(1, DefaultCurrency.PLN, date, date);

        // Assert
        Assert.Single(aggregated);
        Assert.Equal(15m, aggregated[0].Value);
        Assert.Equal(date, aggregated[0].DateTime);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_WithInvestmentType_AggregatesValues()
    {
        // Arrange
        var date = new DateTime(2020, 1, 1);
        _mockService1.Setup(x => x.GetAssetsTimeSeries(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), InvestmentType.Stock))
            .ReturnsAsync([new TimeSeriesModel(date, 7)]);
        _mockService2.Setup(x => x.GetAssetsTimeSeries(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), InvestmentType.Stock))
            .ReturnsAsync([new TimeSeriesModel(date, 3)]);

        // Act
        var aggregated = await _assetsService.GetAssetsTimeSeries(1, DefaultCurrency.PLN, date, date, InvestmentType.Stock);

        // Assert
        Assert.Single(aggregated);
        Assert.Equal(10m, aggregated[0].Value);
    }
}