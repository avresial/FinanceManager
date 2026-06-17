using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class RecurringTransactionDetectorControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _testUserId = 531;
    private readonly Mock<IRecurringTransactionDetectorService> _serviceMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _serviceMock
            .Setup(x => x.GetRecurringTransactions(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RecurringTransactionResult("Subscription", -12m)]);
        services.AddSingleton(_serviceMock.Object);
    }

    [Fact]
    public async Task Get_ForAuthenticatedUser_ReturnsRecurringTransactions()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/RecurringTransactionDetector/Get/{_testUserId}", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Subscription", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_ForOtherUser_ReturnsForbidden()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync("api/RecurringTransactionDetector/Get/999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _serviceMock.Verify(x => x.GetRecurringTransactions(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}