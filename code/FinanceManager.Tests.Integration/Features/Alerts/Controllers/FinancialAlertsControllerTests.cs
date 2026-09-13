using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Alerts.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public sealed class FinancialAlertsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _testUserId = 735;
    private static readonly Mock<IFinancialAlertService> _serviceMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _serviceMock.Reset();
        services.AddSingleton(_serviceMock.Object);
    }

    [Fact]
    public async Task Get_ForAuthenticatedUser_ReturnsOnlyTheirAlerts()
    {
        var alert = CreateAlert(_testUserId, "Low cash");
        _serviceMock
            .Setup(x => x.GetAlertsAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alert]);
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync("api/FinancialAlerts", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<FinancialAlertDto>>(TestContext.Current.CancellationToken);
        var returned = Assert.Single(result!);
        Assert.Equal(alert.Id, returned.Id);
        Assert.Equal("Low cash", returned.Title);
        _serviceMock.Verify(x => x.GetAlertsAsync(_testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("api/FinancialAlerts", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _serviceMock.Verify(x => x.GetAlertsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ForAuthenticatedUser_ReturnsCreatedAlert()
    {
        Authorize("user", _testUserId, UserRole.User);
        var command = new CreateFinancialAlert(
            "Restaurant spend",
            AlertType.CategorySpending,
            AlertComparisonOperator.GreaterThan,
            1000m,
            AlertEvaluationPeriod.CurrentMonth,
            LabelName: "Restaurants",
            CooldownPeriod: TimeSpan.FromHours(6));
        var created = CreateAlert(_testUserId, command.Title, command.AlertType);
        _serviceMock
            .Setup(x => x.CreateAlertAsync(_testUserId, It.IsAny<CreateFinancialAlert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var response = await Client.PostAsJsonAsync("api/FinancialAlerts", command, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FinancialAlertDto>(TestContext.Current.CancellationToken);
        Assert.Equal(created.Id, result!.Id);
        _serviceMock.Verify(x => x.CreateAlertAsync(
            _testUserId,
            It.Is<CreateFinancialAlert>(x => x.Title == command.Title && x.AlertType == command.AlertType),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithInvalidTitle_ReturnsBadRequestWithoutCallingService()
    {
        Authorize("user", _testUserId, UserRole.User);
        var command = new CreateFinancialAlert(
            " ",
            AlertType.AccountBalance,
            AlertComparisonOperator.LessThan,
            3000m);

        var response = await Client.PostAsJsonAsync("api/FinancialAlerts", command, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.Verify(x => x.CreateAlertAsync(It.IsAny<int>(), It.IsAny<CreateFinancialAlert>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ForAuthenticatedUser_ReturnsUpdatedAlert()
    {
        Authorize("user", _testUserId, UserRole.User);
        var alertId = Guid.NewGuid();
        var command = new UpdateFinancialAlert(
            "Updated alert",
            true,
            AlertComparisonOperator.GreaterThanOrEqual,
            1250m,
            AlertEvaluationPeriod.Last30Days,
            MerchantName: "Market",
            AlertType: AlertType.MerchantSpending);
        var updated = CreateAlert(_testUserId, command.Title, AlertType.MerchantSpending);
        updated.Id = alertId;
        _serviceMock
            .Setup(x => x.UpdateAlertAsync(_testUserId, alertId, It.IsAny<UpdateFinancialAlert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var response = await Client.PutAsJsonAsync($"api/FinancialAlerts/{alertId}", command, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FinancialAlertDto>(TestContext.Current.CancellationToken);
        Assert.Equal(alertId, result!.Id);
        _serviceMock.Verify(x => x.UpdateAlertAsync(
            _testUserId,
            alertId,
            It.Is<UpdateFinancialAlert>(x => x.Title == command.Title && x.AlertType == AlertType.MerchantSpending),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetEnabled_AndDelete_UseAuthenticatedUser()
    {
        Authorize("user", _testUserId, UserRole.User);
        var alertId = Guid.NewGuid();
        _serviceMock
            .Setup(x => x.SetEnabledAsync(_testUserId, alertId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceMock
            .Setup(x => x.DeleteAlertAsync(_testUserId, alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var patch = await Client.PatchAsJsonAsync($"api/FinancialAlerts/{alertId}/enabled", false, TestContext.Current.CancellationToken);
        var delete = await Client.DeleteAsync($"api/FinancialAlerts/{alertId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, patch.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        _serviceMock.Verify(x => x.SetEnabledAsync(_testUserId, alertId, false, It.IsAny<CancellationToken>()), Times.Once);
        _serviceMock.Verify(x => x.DeleteAlertAsync(_testUserId, alertId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Evaluate_ReturnsDeterministicOutcome()
    {
        Authorize("user", _testUserId, UserRole.User);
        var alert = CreateAlert(_testUserId, "Large purchase", AlertType.LargeTransaction);
        var outcome = new AlertEvaluationOutcome(
            alert.Id,
            alert.Title,
            alert.AlertType,
            AlertTriggerStatus.Triggered,
            IsTriggered: true,
            IsNewlyTriggered: true,
            IsSuppressed: false,
            DeDuplicationReason.None,
            CurrentValue: 2500m,
            Threshold: 2000m,
            ComparisonOperator: AlertComparisonOperator.GreaterThan,
            ConditionFingerprint: "large-1",
            Message: "Large transaction",
            EvaluatedAt: DateTime.UtcNow,
            Context: new Dictionary<string, string>());
        _serviceMock
            .Setup(x => x.EvaluateAlertsAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([outcome]);

        var response = await Client.PostAsync("api/FinancialAlerts/evaluate", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AlertEvaluationOutcome>>(TestContext.Current.CancellationToken);
        var returned = Assert.Single(result!);
        Assert.True(returned.IsTriggered);
        Assert.Equal(2500m, returned.CurrentValue);
        _serviceMock.Verify(x => x.EvaluateAlertsAsync(_testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FinancialAlert CreateAlert(int userId, string title, AlertType type = AlertType.AccountBalance) =>
        new(userId, title, type, AlertComparisonOperator.GreaterThan, 100m);
}