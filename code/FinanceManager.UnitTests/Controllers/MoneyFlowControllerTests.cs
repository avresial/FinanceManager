using FinanceManager.Api.Controllers;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
namespace FinanceManager.UnitTests.Controllers;

public class MoneyFlowControllerTests
{
    private const int testUserId = 1;

    private readonly Mock<IMoneyFlowService> _mockmoneyFlowService;
    private readonly MoneyFlowController _controller;

    public MoneyFlowControllerTests()
    {
        _mockmoneyFlowService = new Mock<IMoneyFlowService>();
        _controller = new MoneyFlowController(_mockmoneyFlowService.Object);

        // Mock user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, testUserId.ToString())], "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetEndAssetsPerAcount_ReturnsSingleElement()
    {
        // Arrange
        var startDate = new DateTime(2000, 1, 1);
        var endDate = new DateTime(2000, 2, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetEndAssetsPerAccount(testUserId, DefaultCurrency.Currency, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        IActionResult result = await _controller.GetEndAssetsPerAccount(testUserId, DefaultCurrency.Currency, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<PieChartModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetEndAssetsPerType_ReturnsSingleElement()
    {
        // Arrange
        var startDate = new DateTime(2000, 1, 1);
        var endDate = new DateTime(2000, 2, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetEndAssetsPerType(testUserId, DefaultCurrency.Currency, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        IActionResult result = await _controller.GetEndAssetsPerType(testUserId, DefaultCurrency.Currency, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<PieChartModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsSingleElement()
    {
        // Arrange
        var startDate = new DateTime(2000, 1, 1);
        var endDate = new DateTime(2000, 2, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetAssetsTimeSeries(testUserId, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        IActionResult result = await _controller.GetAssetsTimeSeries(testUserId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetAssetsTimeSeriesByType_ReturnsSingleElement()
    {
        // Arrange
        var startDate = new DateTime(2000, 1, 1);
        var endDate = new DateTime(2000, 2, 1);
        var type = InvestmentType.Cash;
        _mockmoneyFlowService.Setup(repo => repo.GetAssetsTimeSeries(testUserId, startDate, endDate, type)).ReturnsAsync([new()]);

        // Act
        IActionResult result = await _controller.GetAssetsTimeSeries(testUserId, startDate, endDate, type);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetNetWorth_ReturnsNotNull()
    {
        // Arrange
        var date = new DateTime(2000, 1, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetNetWorth(testUserId, date)).ReturnsAsync((decimal)1);

        // Act
        IActionResult result = await _controller.GetNetWorth(testUserId, date);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<decimal>(okResult.Value);
    }

    [Fact]
    public async Task GetNetWorth_ReturnsSingleElement()
    {
        // Arrange
        var startDate = new DateTime(2000, 1, 1);
        var endDate = new DateTime(2000, 2, 1);
        var netWorth = new Dictionary<DateTime, decimal>
        {
            { startDate, 1 }
        };
        _mockmoneyFlowService.Setup(repo => repo.GetNetWorth(testUserId, startDate, endDate)).ReturnsAsync(netWorth);

        // Act
        IActionResult result = await _controller.GetNetWorth(testUserId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<Dictionary<DateTime, decimal>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetIncome_ReturnsSingleElement()
    {
        // Arrange
        var startDate = new DateTime(2000, 1, 1);
        var endDate = new DateTime(2000, 2, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetIncome(testUserId, startDate, endDate, It.IsAny<TimeSpan?>())).ReturnsAsync([new()]);

        // Act
        IActionResult result = await _controller.GetIncome(testUserId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetSpending_ReturnsSingleElement()
    {
        // Arrange
        var startDate = new DateTime(2000, 1, 1);
        var endDate = new DateTime(2000, 2, 1);
        _mockmoneyFlowService.Setup(repo => repo.GetSpending(testUserId, startDate, endDate, It.IsAny<TimeSpan?>())).ReturnsAsync([new()]);

        // Act
        IActionResult result = await _controller.GetSpending(testUserId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }
}
