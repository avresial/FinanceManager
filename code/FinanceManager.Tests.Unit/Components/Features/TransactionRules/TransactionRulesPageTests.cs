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
    public async Task EmptyRules_HidesApplyAndPreviewSections()
    {
        var handler = new RulesHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No automation rules yet", cut.Markup);
            Assert.DoesNotContain("Apply existing rules", cut.Markup);
            Assert.DoesNotContain("Preview rules", cut.Markup);
        });
    }

    [Fact]
    public async Task CreatingFirstRule_ShowsApplyAndPreviewSections()
    {
        var handler = new RulesHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("No automation rules yet", cut.Markup));

        var nameLabel = cut.FindAll("label").Single(label => label.TextContent.Contains("Rule name", StringComparison.Ordinal));
        cut.Find($"#{nameLabel.GetAttribute("for")}").Change("First rule");
        cut.FindAll("button").Single(button => button.TextContent.Contains("Create rule", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Apply existing rules", cut.Markup);
            Assert.Contains("Preview rules", cut.Markup);
        });
    }

    [Fact]
    public async Task DeletingFinalRule_HidesApplyAndPreviewSections()
    {
        var handler = new RulesHandler(AccountRule());
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Apply existing rules", cut.Markup));

        cut.Find("button[aria-label='Delete Account rule']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No automation rules yet", cut.Markup);
            Assert.DoesNotContain("Apply existing rules", cut.Markup);
            Assert.DoesNotContain("Preview rules", cut.Markup);
        });
    }

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

    [Fact]
    public async Task Test_UsesUnsavedEditorState_AndShowsBeforeAfterResults()
    {
        var handler = new RulesHandler(AccountRule());
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Account rule", cut.Markup));

        cut.Find("button[aria-label='Edit Account rule']").Click();
        var labelsLabel = cut.FindAll("label").Single(label => label.TextContent == "Labels");
        cut.Find($"#{labelsLabel.GetAttribute("for")}").Change("Salary");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Test").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(handler.LastTest);
            Assert.Equal(["Salary"], handler.LastTest!.Actions.Single().Labels);
            Assert.Contains("Test results", cut.Markup);
            Assert.Contains("PAYPRO", cut.Markup);
            Assert.Contains("Income, Salary", cut.Markup);
        });
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

    private sealed class RulesHandler(TransactionRuleDto? rule = null) : HttpMessageHandler
    {
        private readonly List<TransactionRuleDto> _rules = rule is null ? [] : [rule];

        public UpdateTransactionRule? LastUpdate { get; private set; }
        public CreateTransactionRule? LastTest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_rules) };

            if (request.Method == HttpMethod.Post)
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/test", StringComparison.Ordinal))
                {
                    LastTest = await request.Content!.ReadFromJsonAsync<CreateTransactionRule>(cancellationToken);
                    var result = new TransactionRuleTestResultDto(
                        1,
                        "Cash",
                        10,
                        DateTime.UtcNow,
                        -25m,
                        new("PAYPRO", "Purchase", 1, 25m, TransactionDirection.Expense, ["Income"]),
                        new("PAYPRO", "Purchase", 1, 25m, TransactionDirection.Expense, ["Income", "Salary"]),
                        true);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { result }) };
                }

                var created = AccountRule() with { Name = "First rule" };
                _rules.Add(created);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(created) };
            }

            if (request.Method == HttpMethod.Put)
            {
                LastUpdate = await request.Content!.ReadFromJsonAsync<UpdateTransactionRule>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_rules.Single()) };
            }

            if (request.Method == HttpMethod.Delete)
                _rules.Clear();

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}