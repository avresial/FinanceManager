using Bunit;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.TransactionRules.Components;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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
    public async Task Edit_AccountCondition_ShowsNamesAndPersistsSelectedAccountIds()
    {
        var handler = new RulesHandler(AccountRule(), new AvailableAccount(1, "Main"), new AvailableAccount(2, "Savings"));
        await using var context = CreateContext(handler);
        var popoverProvider = context.Render<MudPopoverProvider>();
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Account rule", cut.Markup));

        cut.Find("button[aria-label='Edit Account rule']").Click();
        var accountSelect = cut.FindComponents<MudSelect<int>>().Single(select => select.Instance.Label == "Accounts");
        await cut.InvokeAsync(accountSelect.Instance.OpenMenu);
        popoverProvider.WaitForAssertion(() =>
        {
            Assert.Contains("Main", popoverProvider.Markup);
            Assert.Contains("Savings", popoverProvider.Markup);
            Assert.DoesNotContain("Account ids", cut.Markup);
        });

        await cut.InvokeAsync(() => accountSelect.Instance.SelectedValuesChanged.InvokeAsync([1, 2]));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.NotNull(handler.LastUpdate));
        Assert.Equal([1, 2], handler.LastUpdate!.Conditions.Single().AccountIds);
    }

    [Fact]
    public async Task Edit_AccountCondition_PreservesDeletedAccountSelection()
    {
        var handler = new RulesHandler(AccountRule(99), new AvailableAccount(1, "Main"));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Account rule", cut.Markup));

        cut.Find("button[aria-label='Edit Account rule']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Deleted account (#99)", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.NotNull(handler.LastUpdate));
        Assert.Equal([99], handler.LastUpdate!.Conditions.Single().AccountIds);
    }

    [Fact]
    public async Task AccountSelectors_DisambiguateDuplicateNames()
    {
        var handler = new RulesHandler(AccountRule(),
            new AvailableAccount(1, "Main", AccountLabel.Cash),
            new AvailableAccount(2, "Main", AccountLabel.Cash));
        await using var context = CreateContext(handler);
        var popoverProvider = context.Render<MudPopoverProvider>();
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Account rule", cut.Markup));

        cut.Find("button[aria-label='Edit Account rule']").Click();

        var accountSelect = cut.FindComponents<MudSelect<int>>().Single(select => select.Instance.Label == "Accounts");
        await cut.InvokeAsync(accountSelect.Instance.OpenMenu);
        popoverProvider.WaitForAssertion(() =>
        {
            Assert.Contains("Main (Cash 1 of 2)", popoverProvider.Markup);
            Assert.Contains("Main (Cash 2 of 2)", popoverProvider.Markup);
        });
    }

    [Fact]
    public async Task Preview_UsesSelectedAccountId()
    {
        var handler = new RulesHandler(AccountRule(), new AvailableAccount(1, "Main"), new AvailableAccount(2, "Savings"));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindComponents<MudSelectItem<int?>>().Count));

        var accountSelect = cut.FindComponents<MudSelect<int?>>().Single(select => select.Instance.Label == "Account");
        await cut.InvokeAsync(() => accountSelect.Instance.ValueChanged.InvokeAsync(2));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Preview", StringComparison.OrdinalIgnoreCase)).Click();

        cut.WaitForAssertion(() => Assert.NotNull(handler.LastPreview));
        Assert.Equal(2, handler.LastPreview!.AccountId);
    }

    [Fact]
    public async Task Create_AccountCondition_UsesSelectedAccountIds()
    {
        var handler = new RulesHandler(AccountRule(), new AvailableAccount(1, "Main"), new AvailableAccount(2, "Savings"));
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Create a rule", cut.Markup));

        var conditionSelect = cut.FindComponents<MudSelect<string>>().Single(select => select.Instance.Label == "Condition");
        await cut.InvokeAsync(() => conditionSelect.Instance.ValueChanged.InvokeAsync("Account"));
        var accountSelect = cut.FindComponents<MudSelect<int>>().Single(select => select.Instance.Label == "Accounts");
        await cut.InvokeAsync(() => accountSelect.Instance.SelectedValuesChanged.InvokeAsync([2]));

        var ruleNameLabel = cut.FindAll("label").Single(label => label.TextContent.Contains("Rule name", StringComparison.Ordinal));
        cut.Find($"#{ruleNameLabel.GetAttribute("for")}").Change("Savings rule");
        cut.FindAll("button").Single(button => button.TextContent.Contains("Create rule", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.NotNull(handler.LastCreate));
        Assert.Equal([2], handler.LastCreate!.Conditions.Single().AccountIds);
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

        cut.Find($"#{labelsLabel.GetAttribute("for")}").Change("Bills");
        cut.WaitForAssertion(() => Assert.DoesNotContain("Test results", cut.Markup));
    }

    private static BunitContext CreateContext(RulesHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };
        context.Services.AddSingleton(new TransactionRuleHttpClient(httpClient));
        context.Services.AddSingleton(new CurrencyAccountHttpClient(httpClient));
        return context;
    }

    private static TransactionRuleDto AccountRule(int accountId = 1) => new(
        Guid.NewGuid(),
        "Account rule",
        1,
        true,
        false,
        [new() { Type = "Account", AccountIds = [accountId] }],
        [new() { Type = "SetLabels", Labels = ["Bills"] }],
        DateTime.UtcNow,
        null);

    private sealed class RulesHandler(TransactionRuleDto? rule = null, params AvailableAccount[] accounts) : HttpMessageHandler
    {
        private readonly List<TransactionRuleDto> _rules = rule is null ? [] : [rule];

        public UpdateTransactionRule? LastUpdate { get; private set; }
        public CreateTransactionRule? LastCreate { get; private set; }
        public TransactionRulePreviewFacts? LastPreview { get; private set; }
        public CreateTransactionRule? LastTest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/CurrencyAccount", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(accounts.Length == 0 ? [new AvailableAccount(1, "Main")] : accounts)
                };

            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_rules) };

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/preview", StringComparison.Ordinal))
            {
                LastPreview = await request.Content!.ReadFromJsonAsync<TransactionRulePreviewFacts>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionRuleEngineResult()) };
            }

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

                LastCreate = await request.Content!.ReadFromJsonAsync<CreateTransactionRule>(cancellationToken);
                var created = AccountRule() with { Name = LastCreate!.Name };
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