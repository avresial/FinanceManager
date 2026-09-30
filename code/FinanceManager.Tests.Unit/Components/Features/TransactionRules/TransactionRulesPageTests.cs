using Bunit;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.TransactionRules.Components;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Conditions;
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
    public async Task AccountLoadFailure_StillShowsRulesWithoutDeletedAccountLabel()
    {
        var handler = new RulesHandler(AccountRule()) { FailAccountLoad = true };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Account rule", cut.Markup);
            Assert.Contains("Unable to load accounts", cut.Markup);
        });

        cut.Find(".rule-row").Click();
        cut.Find("button[aria-label='Edit Account rule']").Click();
        Assert.Contains("Account #1", cut.Markup);
        Assert.DoesNotContain("Deleted account (#1)", cut.Markup);
    }

    [Fact]
    public async Task EmptyRules_HidesApplyAndPreviewSections()
    {
        var handler = new RulesHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No automation rules yet", cut.Markup);
            Assert.DoesNotContain("Apply to existing currency transactions", cut.Markup);
            Assert.DoesNotContain("Test the full sequence", cut.Markup);
        });
    }

    [Fact]
    public async Task CreatingFirstRule_ShowsApplyAndPreviewSections()
    {
        var handler = new RulesHandler();
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("No automation rules yet", cut.Markup));

        cut.Find("button[aria-label='Create a new rule']").Click();
        var nameLabel = cut.FindAll("label").Single(label => label.TextContent.Contains("Rule name", StringComparison.Ordinal));
        cut.Find($"#{nameLabel.GetAttribute("for")}").Change("First rule");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Create rule" && button.GetAttribute("aria-label") is null).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Apply to existing currency transactions", cut.Markup);
            Assert.Contains("Test the full sequence", cut.Markup);
        });
    }

    [Fact]
    public async Task DeletingFinalRule_HidesApplyAndPreviewSections()
    {
        var handler = new RulesHandler(AccountRule());
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Apply to existing currency transactions", cut.Markup));

        cut.Find(".rule-row").Click();
        cut.Find("button[aria-label='Delete Account rule']").Click();
        Assert.False(handler.Deleted);
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Confirm delete").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No automation rules yet", cut.Markup);
            Assert.DoesNotContain("Apply to existing currency transactions", cut.Markup);
            Assert.DoesNotContain("Test the full sequence", cut.Markup);
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

        cut.Find(".rule-row").Click();
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

        cut.Find(".rule-row").Click();
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

        cut.Find(".rule-row").Click();
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
        cut.WaitForAssertion(() => Assert.Contains("Account rule", cut.Markup));
        cut.Find("button[aria-label='Create a new rule']").Click();

        var conditionSelect = cut.FindComponents<MudSelect<string>>().Single(select => select.Instance.Label == "Condition");
        await cut.InvokeAsync(() => conditionSelect.Instance.ValueChanged.InvokeAsync("Account"));
        var accountSelect = cut.FindComponents<MudSelect<int>>().Single(select => select.Instance.Label == "Accounts");
        await cut.InvokeAsync(() => accountSelect.Instance.SelectedValuesChanged.InvokeAsync([2]));

        var ruleNameLabel = cut.FindAll("label").Single(label => label.TextContent.Contains("Rule name", StringComparison.Ordinal));
        cut.Find($"#{ruleNameLabel.GetAttribute("for")}").Change("Savings rule");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Create rule" && button.GetAttribute("aria-label") is null).Click();

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

        cut.Find(".rule-row").Click();
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

        cut.Find(".rule-row").Click();
        cut.Find("button[aria-label='Edit Account rule']").Click();
        var labelsLabel = cut.FindAll("label").Single(label => label.TextContent == "Labels");
        cut.Find($"#{labelsLabel.GetAttribute("for")}").Change("Salary");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Test").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(handler.LastTest);
            Assert.Equal(["Salary"], handler.LastTest!.Actions.Single().Labels);
            Assert.Contains("Draft test results", cut.Markup);
            Assert.Contains("PAYPRO", cut.Markup);
            Assert.Contains("Income, Salary", cut.Markup);
        });

        cut.Find($"#{labelsLabel.GetAttribute("for")}").Change("Bills");
        cut.WaitForAssertion(() => Assert.DoesNotContain("Draft test results", cut.Markup));
    }

    [Fact]
    public async Task DefaultView_ShowsExecutionOrderAndRealValues_WithoutEditor()
    {
        var rule = DetailedRule();
        var handler = new RulesHandler(rule) { AdditionalRules = [AccountRule() with { Name = "Earlier", Order = 1 }] };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rule-row").Count));

        Assert.Contains("Earlier", cut.FindAll(".rule-row")[0].TextContent);
        Assert.Contains("01", cut.FindAll(".rule-row")[0].TextContent);
        Assert.Contains("02", cut.FindAll(".rule-row")[1].TextContent);
        Assert.Contains("Contractor contains “ACME”", cut.FindAll(".rule-row")[1].TextContent);
        Assert.Contains("Rename contractor to “Acme”", cut.FindAll(".rule-row")[1].TextContent);
        Assert.Contains("Replace existing labels with Business", cut.FindAll(".rule-row")[1].TextContent);
        Assert.Empty(cut.FindAll(".rule-editor"));
    }

    [Fact]
    public async Task Expansion_IsInlineExclusiveAndCollapsible_WithAccessibleState()
    {
        var handler = new RulesHandler(DetailedRule()) { AdditionalRules = [AccountRule()] };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rule-row").Count));

        cut.FindAll(".rule-row")[1].Click();
        var expanded = cut.Find(".rule-row[aria-expanded='true']");
        Assert.Equal("button", expanded.TagName.ToLowerInvariant());
        Assert.Equal("button", expanded.GetAttribute("type"));
        Assert.Equal(expanded.GetAttribute("aria-controls"), cut.Find(".rule-detail").Id);
        Assert.Equal(expanded.ParentElement, cut.Find(".rule-detail").ParentElement);
        Assert.Contains("All conditions must match", cut.Find(".rule-detail").TextContent);
        Assert.Contains("ignore case", cut.Find(".rule-detail").TextContent);
        Assert.Contains($"Expense amount at least {10m:N2} and at most {50m:N2} (inclusive)", cut.Find(".rule-detail").TextContent);
        Assert.Equal(2, cut.FindAll(".action-list li").Count);
        Assert.Contains("Stop after match: On", cut.Find(".rule-detail").TextContent);
        Assert.Contains("actions first", cut.Find(".rule-detail").TextContent);

        cut.FindAll(".rule-row")[0].Click();
        Assert.Single(cut.FindAll(".rule-detail"));
        Assert.Contains("Account rule", cut.Find(".rule-detail").TextContent);
        cut.FindAll(".rule-row")[0].Click();
        Assert.Empty(cut.FindAll(".rule-detail"));
        Assert.All(cut.FindAll(".rule-row"), row => Assert.Equal("false", row.GetAttribute("aria-expanded")));
    }

    [Fact]
    public async Task Edit_StaysInsideSelectedRow_AndCancelKeepsItsDetailOpen()
    {
        var handler = new RulesHandler(AccountRule());
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));
        cut.Find(".rule-row").Click();
        cut.Find("button[aria-label='Edit Account rule']").Click();
        Assert.Single(cut.FindAll(".rule-detail .rule-editor"));
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").Click();
        Assert.Empty(cut.FindAll(".rule-editor"));
        Assert.Single(cut.FindAll(".rule-detail"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_PersistsAndRenumbersOnSuccess_PreservesOrderOnFailure(bool fails)
    {
        var first = AccountRule();
        var second = DetailedRule();
        var handler = new RulesHandler(first) { AdditionalRules = [second], FailReorder = fails };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rule-row").Count));
        cut.FindAll(".rule-row")[0].Click();
        Assert.True(cut.Find("button[aria-label='Move Account rule up']").HasAttribute("disabled"));
        cut.Find("button[aria-label='Move Account rule down']").Click();
        cut.WaitForAssertion(() => Assert.Equal([second.Id, first.Id], handler.LastReorder));
        cut.WaitForAssertion(() => Assert.Equal(fails, cut.Find("button[aria-label='Move Account rule up']").HasAttribute("disabled")));
        var rows = cut.FindAll(".rule-row");
        Assert.Contains(fails ? first.Name : second.Name, rows[0].TextContent);
        Assert.Contains("01", rows[0].TextContent);
        Assert.Contains("02", rows[1].TextContent);
        Assert.Single(cut.FindAll(".rule-detail"));
    }

    [Fact]
    public async Task DisabledSavedRule_TestClearlyShowsDisabled_WithoutApplyingActions()
    {
        var handler = new RulesHandler(AccountRule() with { IsEnabled = false }) { TestResults = [] };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));
        cut.Find(".rule-row").Click();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Test this rule").Click();
        cut.WaitForAssertion(() => Assert.Contains("Disabled rule: no actions are applied.", cut.Markup));
        Assert.False(handler.LastTest!.IsEnabled);
        Assert.DoesNotContain("No matching transactions", cut.Markup);
        Assert.Null(handler.LastUpdate);
        Assert.Null(handler.LastCreate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavedRule_TestShowsNoMatchOrNoEffectiveChange(bool noMatches)
    {
        var unchanged = TestResult() with { After = TestResult().Before, HasChanges = false };
        var handler = new RulesHandler(AccountRule()) { TestResults = noMatches ? [] : [unchanged] };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));
        cut.Find(".rule-row").Click();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Test this rule").Click();
        cut.WaitForAssertion(() => Assert.Contains(noMatches ? "No matching transactions found." : "Rule matches but produces no effective change.", cut.Markup));
        Assert.Null(handler.LastCreate);
        Assert.Null(handler.LastUpdate);
    }

    [Fact]
    public async Task PendingTest_CannotPopulateAnotherRulesWorkspace()
    {
        var completion = new TaskCompletionSource<List<TransactionRuleTestResultDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RulesHandler(AccountRule()) { AdditionalRules = [DetailedRule()], TestCompletion = completion };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rule-row").Count));
        cut.FindAll(".rule-row")[0].Click();
        var test = cut.FindAll("button").Single(button => button.TextContent.Trim() == "Test this rule").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotNull(handler.LastTest));
        cut.FindAll(".rule-row")[1].Click();
        completion.SetResult([TestResult()]);
        await test;
        Assert.DoesNotContain("PAYPRO", cut.Find(".rule-preview").TextContent);
        Assert.Contains("Run a test", cut.Find(".rule-preview").TextContent);
    }

    [Fact]
    public async Task PendingDraftTest_IsInvalidatedWhenEditorChanges()
    {
        var completion = new TaskCompletionSource<List<TransactionRuleTestResultDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RulesHandler(AccountRule()) { TestCompletion = completion };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));
        cut.Find(".rule-row").Click();
        cut.Find("button[aria-label='Edit Account rule']").Click();
        var test = cut.FindAll("button").Single(button => button.TextContent.Trim() == "Test").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotNull(handler.LastTest));
        var nameLabel = cut.FindAll("label").Single(label => label.TextContent == "Rule name");
        cut.Find($"#{nameLabel.GetAttribute("for")}").Change("Changed while testing");
        completion.SetResult([TestResult()]);
        await test;
        Assert.DoesNotContain("Draft test results", cut.Markup);
    }

    [Fact]
    public async Task SequencePreview_ShowsEveryOutcomeAndBeforeAfter_ThenInvalidatesOnInputChange()
    {
        var facts = TestResult();
        var outcomes = Enum.GetValues<TransactionRuleOutcomeStatus>()
            .Select(status => new TransactionRuleOutcome(Guid.NewGuid(), status.ToString(), status, false, null, null, null, status == TransactionRuleOutcomeStatus.StoppedProcessing)).ToList();
        var handler = new RulesHandler(AccountRule()) { PreviewResult = new(facts.Before, facts.After, outcomes, null) };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Preview sequence").Click();
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".sequence-trail li").Count));
        Assert.Contains("Disabled · not evaluated", cut.Markup);
        Assert.Contains("No match", cut.Markup);
        Assert.Contains("Skipped after an earlier rule stopped processing", cut.Markup);
        Assert.Contains("stopped after actions", cut.Markup);
        Assert.Contains("no effective change", cut.Markup);
        Assert.Contains("Income, Salary", cut.Find(".sequence-results").TextContent);
        var field = cut.FindComponents<MudTextField<string>>().Single(component => component.Instance.Label == "Contractor");
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("Other"));
        Assert.Empty(cut.FindAll(".sequence-results"));
    }

    [Fact]
    public async Task Apply_RequiresExplicitConfirmation_AndSendsConfirmedCommand()
    {
        var handler = new RulesHandler(AccountRule());
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".rule-row")));
        var button = cut.FindAll("button").Single(button => button.TextContent.Trim() == "Apply to all currency transactions");
        Assert.True(button.HasAttribute("disabled"));
        Assert.Null(handler.LastApply);
        var confirm = cut.FindComponents<MudCheckBox<bool>>().Single();
        await cut.InvokeAsync(() => confirm.Instance.ValueChanged.InvokeAsync(true));
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Apply to all currency transactions").Click();
        cut.WaitForAssertion(() => Assert.NotNull(handler.LastApply));
        Assert.True(handler.LastApply!.Confirmed);
    }

    [Fact]
    public async Task RuleLoadFailure_ShowsError_InsteadOfEmptyState()
    {
        var handler = new RulesHandler() { FailRuleLoad = true };
        await using var context = CreateContext(handler);
        var cut = context.Render<TransactionRulesPage>();
        cut.WaitForAssertion(() => Assert.Contains("Unable to load transaction automation rules.", cut.Markup));
        Assert.DoesNotContain("No automation rules yet", cut.Markup);
    }

    private static TransactionRuleDto DetailedRule() => new(
        Guid.NewGuid(), "Clean up ACME", 2, true, true,
        [new() { Type = "Contractor", Pattern = "ACME" }, new() { Type = "Amount", MinAmount = 10, MaxAmount = 50 }],
        [new() { Type = "NormalizeContractor", Value = "Acme" }, new() { Type = "SetLabels", Labels = ["Business"], ReplaceExisting = true }],
        DateTime.UtcNow, null);

    private static TransactionRuleTestResultDto TestResult() => new(
        1, "Cash", 10, DateTime.UtcNow, -25m,
        new("PAYPRO", "Purchase", 1, 25m, TransactionDirection.Expense, ["Income"]),
        new("PAYPRO", "Purchase", 1, 25m, TransactionDirection.Expense, ["Income", "Salary"]), true);

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
        public bool FailAccountLoad { get; init; }
        public bool FailRuleLoad { get; init; }
        public bool FailReorder { get; init; }
        public bool Deleted { get; private set; }
        public List<TransactionRuleDto> AdditionalRules { get; init; } = [];
        public IReadOnlyList<Guid>? LastReorder { get; private set; }
        public List<TransactionRuleTestResultDto>? TestResults { get; init; }
        public TaskCompletionSource<List<TransactionRuleTestResultDto>>? TestCompletion { get; init; }
        public TransactionRuleEngineResult PreviewResult { get; init; } = new();
        public ApplyTransactionRules? LastApply { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/CurrencyAccount", StringComparison.Ordinal))
                return new HttpResponseMessage(FailAccountLoad ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                {
                    Content = FailAccountLoad
                        ? new StringContent("unavailable")
                        : JsonContent.Create(accounts.Length == 0 ? [new AvailableAccount(1, "Main")] : accounts)
                };

            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(FailRuleLoad ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(_rules.Concat(AdditionalRules).ToList()) };

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/preview", StringComparison.Ordinal))
            {
                LastPreview = await request.Content!.ReadFromJsonAsync<TransactionRulePreviewFacts>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(PreviewResult) };
            }

            if (request.Method == HttpMethod.Post)
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/test", StringComparison.Ordinal))
                {
                    LastTest = await request.Content!.ReadFromJsonAsync<CreateTransactionRule>(cancellationToken);
                    var results = TestCompletion is not null ? await TestCompletion.Task : TestResults ?? [TestResult()];
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(results) };
                }

                if (request.RequestUri!.AbsolutePath.EndsWith("/reorder", StringComparison.Ordinal))
                {
                    var reorder = await request.Content!.ReadFromJsonAsync<ReorderTransactionRules>(cancellationToken);
                    LastReorder = reorder!.RuleIds;
                    var all = _rules.Concat(AdditionalRules).ToList();
                    var reordered = reorder.RuleIds.Select((id, index) => all.Single(rule => rule.Id == id) with { Order = index + 1 }).ToList();
                    if (!FailReorder)
                    {
                        _rules.Clear();
                        _rules.AddRange(reordered);
                        AdditionalRules.Clear();
                    }
                    return new HttpResponseMessage(FailReorder ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(reordered) };
                }
                if (request.RequestUri!.AbsolutePath.EndsWith("/apply", StringComparison.Ordinal))
                {
                    LastApply = await request.Content!.ReadFromJsonAsync<ApplyTransactionRules>(cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new TransactionRuleApplyResultDto(5, 1)) };
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
            {
                Deleted = true;
                _rules.Clear();
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}