using FinanceManager.Api.Controllers;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
namespace FinanceManager.UnitTests.Api.Controllers;

public class MoneyFlowControllerTests
{
    private const int testUserId = 1;

    private readonly Mock<IMoneyFlowService> _mockmoneyFlowService;
    private readonly MoneyFlowController _controller;

    public MoneyFlowControllerTests()
    {
        _mockmoneyFlowService = new Mock<IMoneyFlowService>();
        var currencyRepository = new CurrencyRepository();
        _controller = new MoneyFlowController(_mockmoneyFlowService.Object, currencyRepository);

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
    public async Task GetIncome_ReturnsSingleElement()
    {
        // Arrange
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetIncome(testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetIncome(testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetSpending_ReturnsSingleElement()
    {
        // Arrange
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetSpending(testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetSpending(testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }
}