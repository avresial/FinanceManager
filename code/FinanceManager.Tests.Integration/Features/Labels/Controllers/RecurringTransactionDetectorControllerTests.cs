using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Labels.Controllers;

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
        _serviceMock
            .Setup(x => x.UpdateSubscription(
                _testUserId,
                It.IsAny<Guid>(),
                It.IsAny<UpdateRecurringSubscription>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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

    [Fact]
    public async Task Update_ForAuthenticatedUser_UpdatesAnnotation()
    {
        Authorize("user", _testUserId, UserRole.User);
        var patternId = Guid.NewGuid();

        var response = await Client.PutAsJsonAsync(
            $"api/RecurringTransactionDetector/{_testUserId}/{patternId}",
            new UpdateRecurringSubscription(true, false, true),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _serviceMock.Verify(x => x.UpdateSubscription(
            _testUserId,
            patternId,
            new UpdateRecurringSubscription(true, false, true),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_ForOtherUser_ReturnsForbidden()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.PutAsJsonAsync(
            $"api/RecurringTransactionDetector/999/{Guid.NewGuid()}",
            new UpdateRecurringSubscription(true, false, false),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _serviceMock.Verify(x => x.UpdateSubscription(
            It.IsAny<int>(),
            It.IsAny<Guid>(),
            It.IsAny<UpdateRecurringSubscription>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}