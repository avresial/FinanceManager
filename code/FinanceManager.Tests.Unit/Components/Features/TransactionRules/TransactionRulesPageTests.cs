using Bunit;
using FinanceManager.Components.Features.TransactionRules.Components;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.TransactionRules;

[Trait("Category", "Unit")]
public sealed class TransactionRulesPageTests
{
    [Fact]
    public async Task Edit_AccountCondition_IgnoresEmptyAccountIdTokens()
    {
        var handler = new RulesHandler(AccountRule());
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Account rule", cut.Markup));

        cut.Find("button[aria-label='Edit Account rule']").Click();
        var accountIdsLabel = cut.FindAll("label").Single(label => label.TextContent.Contains("Account ids", StringComparison.Ordinal));
        cut.Find($"#{accountIdsLabel.GetAttribute("for")}").Change("1, , 2,,");
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.NotNull(handler.LastUpdate));
        Assert.Equal([1, 2], handler.LastUpdate!.Conditions.Single().AccountIds);
    }

    [Theory]
    [InlineData("1, invalid", "'invalid' is not a valid account ID.")]
    [InlineData("1, 0", "'0' is not a valid account ID.")]
    public async Task Edit_AccountCondition_RejectsInvalidNonEmptyAccountIdTokens(string accountIds, string expectedError)
    {
        var handler = new RulesHandler(AccountRule());
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Account rule", cut.Markup));

        cut.Find("button[aria-label='Edit Account rule']").Click();
        var accountIdsLabel = cut.FindAll("label").Single(label => label.TextContent.Contains("Account ids", StringComparison.Ordinal));
        cut.Find($"#{accountIdsLabel.GetAttribute("for")}").Change(accountIds);
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains(expectedError, cut.Markup));
        Assert.Null(handler.LastUpdate);
    }

    [Fact]
    public async Task Edit_RendersAllConditionsAndActionsAndSupportsAddingMore()
    {
        var rule = new TransactionRuleDto(
            Guid.NewGuid(),
            "Multi-step rule",
            1,
            true,
            false,
            [
                new() { Type = "Contractor", Pattern = "acme" },
                new() { Type = "Direction", Direction = TransactionDirection.Expense }
            ],
            [
                new() { Type = "NormalizeDescription", Value = "Archive" },
                new() { Type = "SetLabels", Labels = ["Bills"] }
            ],
            DateTime.UtcNow,
            null);

        var handler = new RulesHandler(rule);
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Multi-step rule", cut.Markup));

        cut.Find("button[aria-label='Edit Multi-step rule']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Condition 2", cut.Markup);
            Assert.Contains("Action 2", cut.Markup);
            Assert.Contains("acme", cut.Markup);
            Assert.Contains("Archive", cut.Markup);
        });

        cut.FindAll("button").Single(button => button.TextContent.Contains("Add condition", StringComparison.Ordinal)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("Add action", StringComparison.Ordinal)).Click();

        Assert.Contains("Condition 3", cut.Markup);
        Assert.Contains("Action 3", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => Assert.NotNull(handler.LastUpdate));
        Assert.Equal(3, handler.LastUpdate!.Conditions.Count);
        Assert.Equal(3, handler.LastUpdate.Actions.Count);
    }

    private static BunitContext CreateContext(RulesHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        context.Services.AddSingleton(new TransactionRuleHttpClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        }));
        return context;
    }

    private static TransactionRuleDto AccountRule() => new(
        Guid.NewGuid(),
        "Account rule",
        1,
        true,
        false,
        [new() { Type = "Account", AccountIds = [1] }],
        [new() { Type = "SetLabels", Labels = ["Bills"] }],
        DateTime.UtcNow,
        null);

    private sealed class RulesHandler(TransactionRuleDto rule) : HttpMessageHandler
    {
        public UpdateTransactionRule? LastUpdate { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { rule }) };

            if (request.Method == HttpMethod.Put)
            {
                LastUpdate = await request.Content!.ReadFromJsonAsync<UpdateTransactionRule>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(rule) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(rule) };
        }
    }
}