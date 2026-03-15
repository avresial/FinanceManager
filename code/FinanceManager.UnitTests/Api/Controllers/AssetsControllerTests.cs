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
public class AssetsControllerTests
{
    private const int TestUserId = 1;

    private readonly Mock<IAssetsService> _assetsServiceMock = new();
    private readonly Mock<IInvestmentPaycheckEstimatorService> _investmentPaycheckEstimatorServiceMock = new();
    private readonly Mock<ICurrencyRepository> _currencyRepositoryMock = new();
    private readonly AssetsController _controller;

    public AssetsControllerTests()
    {
        _currencyRepositoryMock
            .Setup(repo => repo.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(new[] { DefaultCurrency.PLN, DefaultCurrency.USD }.ToAsyncEnumerable());

        _controller = new AssetsController(_assetsServiceMock.Object, _investmentPaycheckEstimatorServiceMock.Object, _currencyRepositoryMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString())], "mock"))
                }
            }
        };
    }

    [Fact]
    public async Task GetInvestmentPaycheckEstimate_ReturnsEstimate()
    {
        var asOfDate = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);
        var expected = new InvestmentPaycheckEstimate
        {
            AsOfDate = asOfDate,
            AnnualWithdrawalRate = 0.05m,
            InvestableAssetsValue = 10000m,
            SustainableMonthlyPaycheck = 41.67m,
            SalaryMonthsRequested = 3,
            SalaryMonthsUsed = 2,
            AverageMonthlySalary = 2500m,
            IncomeReplacementRatio = 0.0167m,
        };

        _investmentPaycheckEstimatorServiceMock
            .Setup(x => x.GetEstimate(TestUserId, DefaultCurrency.PLN, asOfDate, 0.05m, 3))
            .ReturnsAsync(expected);

        var result = await _controller.GetInvestmentPaycheckEstimate(TestUserId, DefaultCurrency.PLN.Id, asOfDate, 0.05m, 3, TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<InvestmentPaycheckEstimate>(okResult.Value);
        Assert.Equal(expected.SustainableMonthlyPaycheck, returnValue.SustainableMonthlyPaycheck);
        Assert.Equal(expected.SalaryMonthsUsed, returnValue.SalaryMonthsUsed);
    }
}