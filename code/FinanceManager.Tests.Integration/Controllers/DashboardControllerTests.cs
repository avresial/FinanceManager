using FinanceManager.Domain.Dtos.Dashboard;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class DashboardControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _testUserId = 733;
    private readonly Mock<IDashboardQueryService> _dashboardQueryServiceMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _dashboardQueryServiceMock
            .Setup(x => x.GetOverview(_testUserId, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardOverviewDto
            {
                UserId = _testUserId,
                ExpenseDistribution = [new NameValueResult("Groceries", -42m)],
            });

        services.AddSingleton(_dashboardQueryServiceMock.Object);
    }

    [Fact]
    public async Task GetOverview_ForAuthenticatedUser_ReturnsOverview()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/Dashboard/overview?userId={_testUserId}&currencyId=0&start={DateTime.UtcNow.Date.AddDays(-7):O}&end={DateTime.UtcNow.Date:O}", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Groceries", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetOverview_ForOtherUser_ReturnsForbidden()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/Dashboard/overview?userId=999&currencyId=0&start={DateTime.UtcNow.Date.AddDays(-7):O}&end={DateTime.UtcNow.Date:O}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _dashboardQueryServiceMock.Verify(x => x.GetOverview(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOverview_WithoutAuthorization_ReturnsUnauthorized()
    {
        ClearAuthorization();

        var response = await Client.GetAsync($"api/Dashboard/overview?userId={_testUserId}&currencyId=0&start={DateTime.UtcNow.Date.AddDays(-7):O}&end={DateTime.UtcNow.Date:O}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}