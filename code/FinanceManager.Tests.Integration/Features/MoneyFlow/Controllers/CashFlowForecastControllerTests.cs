using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

[Trait("Category", "Integration")]
public class CashFlowForecastControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _testUserId = 551;
    private static readonly Mock<ICashFlowForecastService> _serviceMock = new();
    private static readonly Mock<ICurrencyRepository> _currencyRepositoryMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _serviceMock.Reset();
        _currencyRepositoryMock.Reset();

        _currencyRepositoryMock
            .Setup(x => x.GetCurrency(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.USD);
        _serviceMock
            .Setup(x => x.GetForecast(_testUserId, It.IsAny<Currency>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashFlowForecast
            {
                UserId = _testUserId,
                CurrencyId = 1,
                Currency = DefaultCurrency.USD.ShortName,
                HorizonDays = CashFlowForecastHorizons.NinetyDays,
                HasForecastableActivity = true,
                ForecastSeries = [new TimeSeriesModel(DateTime.UtcNow.Date, 1234m)],
                ExpectedTransactions =
                [
                    new CashFlowForecastTransaction
                    {
                        Date = DateTime.UtcNow.Date.AddDays(7),
                        Description = "Salary",
                        Amount = 500m
                    }
                ]
            });

        services.AddSingleton(_currencyRepositoryMock.Object);
        services.AddSingleton(_serviceMock.Object);
    }

    [Fact]
    public async Task Get_ForAuthenticatedUser_ReturnsForecast()
    {
        Authorize("user", _testUserId, UserRole.User);

        var result = await Client.GetFromJsonAsync<CashFlowForecast>(
            $"api/CashFlowForecast?userId={_testUserId}&currencyId=1&horizonDays=90",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(_testUserId, result.UserId);
        Assert.Contains(result.ExpectedTransactions, transaction => transaction.Description == "Salary");
        _serviceMock.Verify(x => x.GetForecast(
            _testUserId,
            DefaultCurrency.USD,
            CashFlowForecastHorizons.NinetyDays,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_ForOtherUser_ReturnsForbidden()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            "api/CashFlowForecast?userId=999&currencyId=1&horizonDays=90",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _serviceMock.Verify(x => x.GetForecast(
            It.IsAny<int>(),
            It.IsAny<Currency>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_WithUnsupportedHorizon_ReturnsBadRequest()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            $"api/CashFlowForecast?userId={_testUserId}&currencyId=1&horizonDays=45",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.Verify(x => x.GetForecast(
            It.IsAny<int>(),
            It.IsAny<Currency>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_WithUnknownCurrency_ReturnsNotFound()
    {
        Authorize("user", _testUserId, UserRole.User);
        _currencyRepositoryMock
            .Setup(x => x.GetCurrency(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);

        var response = await Client.GetAsync(
            $"api/CashFlowForecast?userId={_testUserId}&currencyId=999&horizonDays=90",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _serviceMock.Verify(x => x.GetForecast(
            It.IsAny<int>(),
            It.IsAny<Currency>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}