using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Models;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.TransactionRules.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public sealed class TransactionRuleControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _testUserId = 734;
    private static readonly Mock<ITransactionRuleService> _serviceMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _serviceMock.Reset();
        services.AddSingleton(_serviceMock.Object);
    }

    [Fact]
    public async Task Get_ForAuthenticatedUser_ReturnsRulesForThatUser()
    {
        Authorize("user", _testUserId, UserRole.User);
        var rule = Rule("Bills");
        _serviceMock
            .Setup(x => x.GetRulesAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([rule]);

        var response = await Client.GetAsync("api/TransactionRules", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<TransactionRuleDto>>(TestContext.Current.CancellationToken);
        Assert.Equal(rule.Id, Assert.Single(result!).Id);
        _serviceMock.Verify(x => x.GetRulesAsync(_testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("api/TransactionRules", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _serviceMock.Verify(x => x.GetRulesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ForAuthenticatedUser_ReturnsCreatedRule()
    {
        Authorize("user", _testUserId, UserRole.User);
        var command = new CreateTransactionRule(
            "Bills",
            [new TransactionRuleConditionDto { Type = "Contractor", Pattern = "acme" }],
            [new TransactionRuleActionDto { Type = "SetLabels", Labels = ["Bills"] }]);
        var created = Rule(command.Name);
        _serviceMock
            .Setup(x => x.CreateRuleAsync(_testUserId, It.IsAny<CreateTransactionRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var response = await Client.PostAsJsonAsync("api/TransactionRules", command, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransactionRuleDto>(TestContext.Current.CancellationToken);
        Assert.Equal(created.Id, result!.Id);
        _serviceMock.Verify(x => x.CreateRuleAsync(
            _testUserId,
            It.Is<CreateTransactionRule>(value => value.Name == command.Name),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_ForAuthenticatedUser_ReturnsUpdatedRule()
    {
        Authorize("user", _testUserId, UserRole.User);
        var id = Guid.NewGuid();
        var command = new UpdateTransactionRule(
            "Updated",
            [new TransactionRuleConditionDto { Type = "Description", Pattern = "invoice" }],
            [new TransactionRuleActionDto { Type = "NormalizeDescription", Value = "Receipt" }]);
        var updated = Rule(command.Name, id);
        _serviceMock
            .Setup(x => x.UpdateRuleAsync(_testUserId, id, It.IsAny<UpdateTransactionRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var response = await Client.PutAsJsonAsync($"api/TransactionRules/{id}", command, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransactionRuleDto>(TestContext.Current.CancellationToken);
        Assert.Equal(id, result!.Id);
        _serviceMock.Verify(x => x.UpdateRuleAsync(
            _testUserId,
            id,
            It.Is<UpdateTransactionRule>(value => value.Name == command.Name),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetEnabled_AndDelete_UseAuthenticatedUser()
    {
        Authorize("user", _testUserId, UserRole.User);
        var id = Guid.NewGuid();
        _serviceMock.Setup(x => x.SetEnabledAsync(_testUserId, id, false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _serviceMock.Setup(x => x.DeleteRuleAsync(_testUserId, id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var enabled = await Client.PatchAsJsonAsync($"api/TransactionRules/{id}/enabled", false, TestContext.Current.CancellationToken);
        var deleted = await Client.DeleteAsync($"api/TransactionRules/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, enabled.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        _serviceMock.Verify(x => x.SetEnabledAsync(_testUserId, id, false, It.IsAny<CancellationToken>()), Times.Once);
        _serviceMock.Verify(x => x.DeleteRuleAsync(_testUserId, id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reorder_AndPreview_ReturnServiceResults()
    {
        Authorize("user", _testUserId, UserRole.User);
        var first = Rule("First");
        var second = Rule("Second");
        _serviceMock
            .Setup(x => x.ReorderAsync(_testUserId, It.IsAny<ReorderTransactionRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([second, first]);
        var preview = new TransactionRuleEngineResult(
            new("ACME", "Invoice", 1, 10m, TransactionDirection.Expense, []),
            new("Acme", "Invoice", 1, 10m, TransactionDirection.Expense, ["Bills"]),
            [],
            null)
        {
            HasChanges = true
        };
        _serviceMock
            .Setup(x => x.PreviewAsync(_testUserId, It.IsAny<TransactionRulePreviewFacts>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var reordered = await Client.PostAsJsonAsync(
            "api/TransactionRules/reorder",
            new ReorderTransactionRules([first.Id, second.Id]),
            TestContext.Current.CancellationToken);
        var previewResponse = await Client.PostAsJsonAsync(
            "api/TransactionRules/preview",
            new TransactionRulePreviewFacts("ACME", "Invoice", 1, 10m, TransactionDirection.Expense, []),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        Assert.Equal([second.Id, first.Id], (await reordered.Content.ReadFromJsonAsync<List<TransactionRuleDto>>(TestContext.Current.CancellationToken))!.Select(x => x.Id));
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True((await previewResponse.Content.ReadFromJsonAsync<TransactionRuleEngineResult>(TestContext.Current.CancellationToken))!.HasChanges);
        _serviceMock.Verify(x => x.ReorderAsync(_testUserId, It.IsAny<ReorderTransactionRules>(), It.IsAny<CancellationToken>()), Times.Once);
        _serviceMock.Verify(x => x.PreviewAsync(_testUserId, It.IsAny<TransactionRulePreviewFacts>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Apply_RequiresServiceConfirmationResult()
    {
        Authorize("user", _testUserId, UserRole.User);
        _serviceMock
            .Setup(x => x.ApplyRetroactivelyAsync(_testUserId, It.IsAny<ApplyTransactionRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionRuleApplyResultDto(12, 3));

        var response = await Client.PostAsJsonAsync(
            "api/TransactionRules/apply",
            new ApplyTransactionRules(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransactionRuleApplyResultDto>(TestContext.Current.CancellationToken);
        Assert.Equal(12, result!.Examined);
        Assert.Equal(3, result.Updated);
        _serviceMock.Verify(x => x.ApplyRetroactivelyAsync(
            _testUserId,
            It.Is<ApplyTransactionRules>(value => value.Confirmed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TransactionRuleDto Rule(string name, Guid? id = null) => new(
        id ?? Guid.NewGuid(),
        name,
        1,
        true,
        false,
        [],
        [],
        DateTime.UtcNow,
        null);
}