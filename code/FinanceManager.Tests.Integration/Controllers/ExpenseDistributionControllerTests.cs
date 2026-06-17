using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class ExpenseDistributionControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _testUserId = 521;
    private readonly Mock<IExpenseDistributionService> _expenseDistributionServiceMock = new();

    protected override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
#pragma warning disable CS0854
        var currencyRepoMock = new Mock<ICurrencyRepository>();
        currencyRepoMock.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Range(0, 1).Select(_ => DefaultCurrency.USD));
#pragma warning restore CS0854

        _expenseDistributionServiceMock
            .Setup(x => x.GetExpenseDistribution(_testUserId, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new NameValueResult("Groceries", -42m)]);

        services.AddSingleton(currencyRepoMock.Object);
        services.AddSingleton(_expenseDistributionServiceMock.Object);
    }

    [Fact]
    public async Task GetExpenseDistribution_ForAuthenticatedUser_ReturnsDistribution()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/ExpenseDistribution/GetExpenseDistribution/{_testUserId}/1/{DateTime.UtcNow.Date.AddDays(-7):O}/{DateTime.UtcNow.Date:O}", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Groceries", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExpenseDistribution_ForOtherUser_ReturnsForbidden()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/ExpenseDistribution/GetExpenseDistribution/999/1/{DateTime.UtcNow.Date.AddDays(-7):O}/{DateTime.UtcNow.Date:O}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _expenseDistributionServiceMock.Verify(x => x.GetExpenseDistribution(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }
}