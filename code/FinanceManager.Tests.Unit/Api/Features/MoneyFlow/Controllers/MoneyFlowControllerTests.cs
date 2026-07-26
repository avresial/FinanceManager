using FinanceManager.Api.Features.MoneyFlow.Controllers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
namespace FinanceManager.Tests.Unit.Api.Features.MoneyFlow.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class MoneyFlowControllerTests
{
    private const int _testUserId = 1;

    private readonly Mock<INetWorthService> _mockNetWorthService;
    private readonly Mock<ILabelsValueService> _mockLabelsValueService;
    private readonly Mock<IInvestmentRateService> _mockInvestmentRateService;
    private readonly Mock<IBalanceService> _mockBalanceService;
    private readonly Mock<ICurrencyRepository> _mockCurrencyRepository;
    private readonly MoneyFlowController _controller;

    public MoneyFlowControllerTests()
    {
        _mockNetWorthService = new Mock<INetWorthService>();
        _mockLabelsValueService = new Mock<ILabelsValueService>();
        _mockInvestmentRateService = new Mock<IInvestmentRateService>();
        _mockBalanceService = new Mock<IBalanceService>();
        _mockCurrencyRepository = new Mock<ICurrencyRepository>();
        _mockCurrencyRepository.Setup(repo => repo.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(new[] { DefaultCurrency.PLN, DefaultCurrency.USD }.ToAsyncEnumerable());
        _controller = new MoneyFlowController(_mockNetWorthService.Object, _mockLabelsValueService.Object,
            _mockInvestmentRateService.Object, _mockBalanceService.Object, _mockCurrencyRepository.Object);

        // Mock user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, _testUserId.ToString())], "mock"));

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
        _mockNetWorthService.Setup(repo => repo.GetNetWorth(_testUserId, DefaultCurrency.PLN, date)).ReturnsAsync(1);

        // Act
        var result = await _controller.GetNetWorth(_testUserId, DefaultCurrency.PLN.Id, date, TestContext.Current.CancellationToken);

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
        _mockNetWorthService.Setup(repo => repo.GetNetWorth(_testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync(netWorth);

        // Act
        var result = await _controller.GetNetWorth(_testUserId, DefaultCurrency.PLN.Id, startDate, endDate, TestContext.Current.CancellationToken);

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
        _mockBalanceService.Setup(repo => repo.GetInflow(_testUserId, DefaultCurrency.PLN, startDate, endDate, accountIds)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetInflow(_testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, accountIds, TestContext.Current.CancellationToken);

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
        _mockBalanceService.Setup(repo => repo.GetOutflow(_testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetOutflow(_testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, null, TestContext.Current.CancellationToken);

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
        _mockBalanceService.Setup(repo => repo.GetClosingBalance(_testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        // Act
        var result = await _controller.GetClosingBalance(_testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, null, TestContext.Current.CancellationToken);

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
        _mockBalanceService.Setup(repo => repo.GetNetCashFlow(_testUserId, DefaultCurrency.PLN, startDate, endDate)).ReturnsAsync([new()]);

        var result = await _controller.GetNetCashFlow(_testUserId, DefaultCurrency.PLN.Id, startDate, endDate, null, null, TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<TimeSeriesModel>>(okResult.Value);
        Assert.Single(returnValue);
    }
}