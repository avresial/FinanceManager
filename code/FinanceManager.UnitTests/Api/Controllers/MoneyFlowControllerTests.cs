using FinanceManager.Api.Controllers;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
namespace FinanceManager.UnitTests.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class MoneyFlowControllerTests
{
    private const int testUserId = 1;

    private readonly Mock<IMoneyFlowService> _mockmoneyFlowService;
    private readonly Mock<IBalanceService> _mockBalanceService;
    private readonly Mock<ICurrencyRepository> _mockCurrencyRepository;
    private readonly MoneyFlowController _controller;

    public MoneyFlowControllerTests()
    {
        _mockmoneyFlowService = new Mock<IMoneyFlowService>();
        _mockBalanceService = new Mock<IBalanceService>();
        _mockCurrencyRepository = new Mock<ICurrencyRepository>();
        _mockCurrencyRepository.Setup(repo => repo.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(new[] { DefaultCurrency.PLN, DefaultCurrency.USD }.ToAsyncEnumerable());
        _controller = new MoneyFlowController(_mockmoneyFlowService.Object, _mockBalanceService.Object, _mockCurrencyRepository.Object);

        // Mock user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, testUserId.ToString())], "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetNetWorth_ReturnsNotNull()
    {
        // Arrange
        DateTime date = new(2000, 1, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetNetWorth(testUserId, DefaultCurrency.PLN, date)).ReturnsAsync(1);

        // Act
        var result = await _controller.GetNetWorth(testUserId, DefaultCurrency.PLN.Id, date, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<decimal>(okResult.Value);
    }

    [Fact]
    public async Task GetNetWorth_ReturnsSingleElement()
    {
        // Arrange
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        Dictionary<DateTime, decimal> netWorth = new()
        {
            { startDate, 1 }
        };
        _mockmoneyFlowService.Setup(repo => repo.GetNetWorth(testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync(netWorth);

        // Act
        var result = await _controller.GetNetWorth(testUserId, DefaultCurrency.PLN.Id, startDate, endDate, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<Dictionary<DateTime, decimal>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetInflow_ReturnsSingleElement()
    {
        // Arrange
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        List<int> accountIds = [7];
        _mockBalanceService.Setup(repo => repo.GetInflow(testUserId, DefaultCurrency.PLN, startDate, endDate, accountIds)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetInflow(testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, accountIds, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetOutflow_ReturnsSingleElement()
    {
        // Arrange
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        _mockBalanceService.Setup(repo => repo.GetOutflow(testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetOutflow(testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, null, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetClosingBalance_ReturnsSingleElement()
    {
        // Arrange
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        _mockBalanceService.Setup(repo => repo.GetClosingBalance(testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetClosingBalance(testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, null, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetNetCashFlow_ReturnsSingleElement()
    {
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        _mockBalanceService.Setup(repo => repo.GetNetCashFlow(testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        var result = await _controller.GetNetCashFlow(testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, null, TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }
}