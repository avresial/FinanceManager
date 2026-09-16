using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Actions;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Tests.Unit.Domain.TransactionRules;

[Trait("Category", "Unit")]
public class TransactionRuleEngineTests
{
    private static TransactionFacts Facts(
        string contractor = "ACME Corp. - March invoice",
        string description = "Invoice #123",
        int accountId = 10,
        decimal amount = 100m,
        TransactionDirection direction = TransactionDirection.Expense,
        IReadOnlyList<string>? labels = null)
    {
        IReadOnlyList<string> labelList = labels ?? [];
        return new TransactionFacts(contractor, description, accountId, amount, direction, labelList);
    }

    private static TransactionRule Rule(int order, params object[] conditionsAndActions)
    {
        var conditions = conditionsAndActions.OfType<ITransactionRuleCondition>().ToArray();
        var actions = conditionsAndActions.OfType<ITransactionRuleAction>().ToArray();
        return new TransactionRule(Guid.NewGuid(), $"Rule {order}", order, conditions, actions);
    }

    [Fact]
    public void Run_ContractorCondition_Contains_MatchesCaseInsensitivelyAndAppliesActions()
    {
        var rule = Rule(1, new ContractorCondition("acme"), new NormalizeDescriptionAction("ACME - March invoice"));

        var result = TransactionRuleEngine.Run([rule], Facts());

        Assert.True(result.HasChanges);
        Assert.Single(result.AppliedRules);
        Assert.Equal(TransactionRuleOutcomeStatus.Applied, result.RuleOutcomes[0].Status);
        Assert.Equal("ACME - March invoice", result.FinalFacts.Description);
        Assert.Equal("ACME Corp. - March invoice", result.FinalFacts.Contractor);
    }

    [Fact]
    public void Run_ContractorCondition_CaseSensitive_DoesNotMatch()
    {
        var rule = Rule(1, new ContractorCondition("ACME", ignoreCase: false), new NormalizeDescriptionAction("x"));

        var result = TransactionRuleEngine.Run([rule], Facts(contractor: "acme corp."));

        Assert.False(result.HasChanges);
        Assert.Equal(TransactionRuleOutcomeStatus.NotMatched, result.RuleOutcomes[0].Status);
        Assert.Equal("Invoice #123", result.FinalFacts.Description);
    }

    [Fact]
    public void Run_DescriptionCondition_RegularExpression_MatchesPattern()
    {
        var rule = Rule(1, new DescriptionCondition("^Invoice #\\d+$", TextMatchOperator.RegularExpression), new SetLabelsAction(["Bills"]));

        var result = TransactionRuleEngine.Run([rule], Facts());

        Assert.True(result.HasChanges);
        IReadOnlyList<string> expected = ["Bills"];
        Assert.Equal(expected, result.FinalFacts.Labels);
    }

    [Theory]
    [InlineData(11, true)]
    [InlineData(5, true)]
    [InlineData(12, false)]
    public void Run_AccountCondition_MatchesOnlyConfiguredAccounts(int accountId, bool expectedMatch)
    {
        var rule = Rule(1, new AccountCondition([5, 11]), new NormalizeContractorAction("Changed"));

        var result = TransactionRuleEngine.Run([rule], Facts(accountId: accountId));

        Assert.Equal(expectedMatch, result.RuleOutcomes[0].Status is TransactionRuleOutcomeStatus.Applied or TransactionRuleOutcomeStatus.StoppedProcessing);
    }

    [Theory]
    [InlineData(TransactionDirection.Expense, 60, true)]
    [InlineData(TransactionDirection.Expense, 50, false)]
    [InlineData(TransactionDirection.Income, 60, false)]
    [InlineData(TransactionDirection.Transfer, 60, false)]
    public void Run_AmountCondition_MatchesDirectionAwareAmounts(TransactionDirection direction, int amount, bool expectedMatch)
    {
        var rule = Rule(1, new AmountCondition(TransactionDirection.Expense, 50m), new SetLabelsAction(["Big"]));

        var result = TransactionRuleEngine.Run([rule], Facts(amount: amount, direction: direction));

        Assert.Equal(expectedMatch, result.HasChanges);
    }

    [Theory]
    [InlineData(AmountComparison.LessThan, 49, true)]
    [InlineData(AmountComparison.LessThan, 50, false)]
    [InlineData(AmountComparison.LessThanOrEqual, 50, true)]
    [InlineData(AmountComparison.Equal, 50, true)]
    [InlineData(AmountComparison.Equal, 51, false)]
    [InlineData(AmountComparison.GreaterThanOrEqual, 50, true)]
    [InlineData(AmountComparison.GreaterThan, 50, false)]
    [InlineData(AmountComparison.GreaterThan, 51, true)]
    public void Run_AmountComparison_ComparesAgainstThreshold(AmountComparison comparison, int amount, bool expectedMatch)
    {
        var rule = Rule(1, new AmountCondition(TransactionDirection.Expense, 50m, comparison), new SetLabelsAction(["X"]));

        var result = TransactionRuleEngine.Run([rule], Facts(amount: amount));

        Assert.Equal(expectedMatch, result.HasChanges);
    }

    [Fact]
    public void Run_DirectionCondition_MatchesExactDirection()
    {
        var rule = Rule(1, new DirectionCondition(TransactionDirection.Income), new SetLabelsAction(["Pay"]));

        var result = TransactionRuleEngine.Run([rule], Facts(direction: TransactionDirection.Expense));

        Assert.False(result.HasChanges);
        Assert.Equal(TransactionRuleOutcomeStatus.NotMatched, result.RuleOutcomes[0].Status);
    }

    [Fact]
    public void Run_MultipleConditions_AllMustMatch()
    {
        var rule = Rule(1,
            new ContractorCondition("acme"),
            new AmountCondition(TransactionDirection.Income, 0m, AmountComparison.GreaterThan));

        var result = TransactionRuleEngine.Run([rule], Facts(direction: TransactionDirection.Expense));

        Assert.False(result.HasChanges);
        Assert.Equal(TransactionRuleOutcomeStatus.NotMatched, result.RuleOutcomes[0].Status);
    }

    [Fact]
    public void Run_RuleWithoutConditions_MatchesEveryTransaction()
    {
        var rule = Rule(1, new SetLabelsAction(["Always"]));

        var result = TransactionRuleEngine.Run([rule], Facts(contractor: "Anything at all"));

        Assert.True(result.HasChanges);
        IReadOnlyList<string> expected = ["Always"];
        Assert.Equal(expected, result.FinalFacts.Labels);
    }

    [Fact]
    public void Run_NoRules_ReturnsUnchangedResult()
    {
        var facts = Facts();

        var result = TransactionRuleEngine.Run([], facts);

        Assert.False(result.HasChanges);
        Assert.False(result.IsStopped);
        Assert.Null(result.StoppedRuleId);
        Assert.Empty(result.RuleOutcomes);
        Assert.Equal(facts, result.FinalFacts);
    }

    [Fact]
    public void Run_MatchingRuleWithoutActions_AppliesWithNoChanges()
    {
        var rule = Rule(1, new ContractorCondition("acme"));

        var result = TransactionRuleEngine.Run([rule], Facts());

        Assert.Equal(TransactionRuleOutcomeStatus.Applied, result.RuleOutcomes[0].Status);
        Assert.False(result.RuleOutcomes[0].HasChanges);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void Run_MultipleMatchingRules_RunInAscendingOrder()
    {
        var laterOrder = Rule(2, new ContractorCondition("acme"), new NormalizeDescriptionAction("From order 2"));
        var earlierOrder = Rule(1, new ContractorCondition("acme"), new NormalizeDescriptionAction("From order 1"));

        var result = TransactionRuleEngine.Run([laterOrder, earlierOrder], Facts());

        Assert.Equal("From order 2", result.FinalFacts.Description);
        Guid[] expected = [earlierOrder.Id, laterOrder.Id];
        Assert.Equal(expected, result.RuleOutcomes.Select(outcome => outcome.RuleId).ToArray());
    }

    [Fact]
    public void Run_RulesWithEqualOrder_KeepInputSequence()
    {
        var first = Rule(5, new ContractorCondition("acme"), new NormalizeDescriptionAction("First"));
        var second = Rule(5, new ContractorCondition("acme"), new NormalizeDescriptionAction("Second"));

        var result = TransactionRuleEngine.Run([first, second], Facts());

        Assert.Equal("Second", result.FinalFacts.Description);
        Guid[] expected = [first.Id, second.Id];
        Assert.Equal(expected, result.RuleOutcomes.Select(outcome => outcome.RuleId).ToArray());
    }

    [Fact]
    public void Run_LaterRuleOverwritesEarlierNormalizations()
    {
        var first = Rule(1, new ContractorCondition("acme"),
            new NormalizeContractorAction("Acme"),
            new NormalizeDescriptionAction("Original description"));
        var second = Rule(2, new DescriptionCondition("Original"), new NormalizeContractorAction("Acme Corp."));

        var result = TransactionRuleEngine.Run([first, second], Facts());

        Assert.Equal("Acme Corp.", result.FinalFacts.Contractor);
        Assert.Equal("Original description", result.FinalFacts.Description);
        Assert.Equal("Acme", result.RuleOutcomes[0].Contractor);
        Assert.Equal("Acme Corp.", result.RuleOutcomes[1].Contractor);
    }

    [Fact]
    public void Run_LabelsAccumulateAcrossRules()
    {
        var first = Rule(1, new ContractorCondition("acme"), new SetLabelsAction(["Rent"]));
        var second = Rule(2, new ContractorCondition("acme"), new SetLabelsAction(["Rent", "Housing"]));

        var result = TransactionRuleEngine.Run([first, second], Facts());

        IReadOnlyList<string> expected = ["Rent", "Housing"];
        Assert.Equal(expected, result.FinalFacts.Labels);
    }

    [Fact]
    public void Run_SetLabels_ReplaceExisting_OverwritesEarlierLabels()
    {
        var first = Rule(1, new ContractorCondition("acme"), new SetLabelsAction(["Rent"]));
        var second = Rule(2, new ContractorCondition("acme"), new SetLabelsAction(["Housing", "Utilities"], replaceExisting: true));

        var result = TransactionRuleEngine.Run([first, second], Facts());

        IReadOnlyList<string> expected = ["Housing", "Utilities"];
        Assert.Equal(expected, result.FinalFacts.Labels);
        IReadOnlyList<string> firstLabels = ["Rent"];
        Assert.Equal(firstLabels, result.RuleOutcomes[0].Labels);
        Assert.Equal(expected, result.RuleOutcomes[1].Labels);
    }

    [Fact]
    public void Run_SetLabels_SkipsLabelsAlreadyPresent()
    {
        var rule = Rule(1, new ContractorCondition("acme"), new SetLabelsAction(["Rent", "Housing"]));

        var result = TransactionRuleEngine.Run([rule], Facts(labels: ["Rent"]));

        IReadOnlyList<string> expected = ["Rent", "Housing"];
        Assert.Equal(expected, result.FinalFacts.Labels);
    }

    [Fact]
    public void Run_SetLabels_TreatsLabelNamesCaseInsensitively()
    {
        var rule = Rule(1, new ContractorCondition("acme"), new SetLabelsAction(["rent"]));

        var result = TransactionRuleEngine.Run([rule], Facts(labels: ["Rent"]));

        Assert.Equal(["Rent"], result.FinalFacts.Labels);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void SetLabelsAction_CanApplyWithoutFacts()
    {
        var labels = new List<string>();

        new SetLabelsAction(["Bills"]).Apply(labels);

        Assert.Equal(["Bills"], labels);
    }

    [Fact]
    public void Run_StopProcessing_LaterRulesAreSkippedAfterStop()
    {
        var first = Rule(1, new ContractorCondition("acme"), new SetLabelsAction(["One"]));
        var stopping = Rule(2, new DescriptionCondition("Invoice"), new NormalizeDescriptionAction("Stopped here"));
        stopping.StopProcessing = true;
        var afterStop = Rule(3, new ContractorCondition("acme"), new NormalizeDescriptionAction("Must not run"));

        var result = TransactionRuleEngine.Run([first, stopping, afterStop], Facts());

        Assert.Equal(TransactionRuleOutcomeStatus.Applied, result.RuleOutcomes[0].Status);
        Assert.Equal(TransactionRuleOutcomeStatus.StoppedProcessing, result.RuleOutcomes[1].Status);
        Assert.Equal(TransactionRuleOutcomeStatus.SkippedAfterStop, result.RuleOutcomes[2].Status);
        Assert.Equal(stopping.Id, result.StoppedRuleId);
        Assert.True(result.IsStopped);
        Assert.Equal("Stopped here", result.FinalFacts.Description);
    }

    [Fact]
    public void Run_NonMatchingStopRule_DoesNotStopTheChain()
    {
        var stopping = Rule(1, new ContractorCondition("never appears"), new NormalizeDescriptionAction("x"));
        stopping.StopProcessing = true;
        var later = Rule(2, new ContractorCondition("acme"), new NormalizeDescriptionAction("Ran anyway"));

        var result = TransactionRuleEngine.Run([stopping, later], Facts());

        Assert.Equal(TransactionRuleOutcomeStatus.NotMatched, result.RuleOutcomes[0].Status);
        Assert.Equal(TransactionRuleOutcomeStatus.Applied, result.RuleOutcomes[1].Status);
        Assert.False(result.IsStopped);
        Assert.Null(result.StoppedRuleId);
        Assert.Equal("Ran anyway", result.FinalFacts.Description);
    }

    [Fact]
    public void Run_DisabledRule_IsSkippedWithoutEvaluation()
    {
        var disabled = Rule(1, new ContractorCondition("acme"), new NormalizeDescriptionAction("Disabled change"));
        disabled.IsEnabled = false;
        var enabled = Rule(2, new ContractorCondition("acme"), new NormalizeDescriptionAction("Enabled change"));

        var result = TransactionRuleEngine.Run([disabled, enabled], Facts());

        Assert.Equal(TransactionRuleOutcomeStatus.SkippedDisabled, result.RuleOutcomes[0].Status);
        Assert.Equal(TransactionRuleOutcomeStatus.Applied, result.RuleOutcomes[1].Status);
        Assert.Equal("Enabled change", result.FinalFacts.Description);
    }

    [Fact]
    public void Run_AllRulesDisabled_NoChanges()
    {
        var first = Rule(1, new ContractorCondition("acme"), new SetLabelsAction(["A"]));
        first.IsEnabled = false;
        var second = Rule(2, new ContractorCondition("acme"), new NormalizeContractorAction("B"));
        second.IsEnabled = false;

        var result = TransactionRuleEngine.Run([first, second], Facts());

        Assert.False(result.HasChanges);
        Assert.All(result.RuleOutcomes, outcome => Assert.Equal(TransactionRuleOutcomeStatus.SkippedDisabled, outcome.Status));
    }

    [Fact]
    public void Run_DoesNotMutateTheInputFacts()
    {
        var facts = Facts(labels: ["Existing"]);
        var rule = Rule(1, new ContractorCondition("acme"),
            new NormalizeContractorAction("New"),
            new SetLabelsAction(["Added"], replaceExisting: true));

        var result = TransactionRuleEngine.Run([rule], facts);

        Assert.True(result.HasChanges);
        Assert.Equal("ACME Corp. - March invoice", facts.Contractor);
        Assert.Equal("Invoice #123", facts.Description);
        IReadOnlyList<string> expected = ["Existing"];
        Assert.Equal(expected, facts.Labels);
    }

    [Fact]
    public void Run_SameRulesAndFacts_ProducesIdenticalResults()
    {
        var rules = new[]
        {
            Rule(1, new ContractorCondition("acme"), new NormalizeContractorAction("Acme"), new SetLabelsAction(["Rent"])),
            Rule(2, new DescriptionCondition("Invoice"), new NormalizeDescriptionAction("ACME invoice")),
        };
        var facts = Facts();

        var first = TransactionRuleEngine.Run(rules, facts);
        var second = TransactionRuleEngine.Run(rules, facts);

        Assert.Equal(first.FinalFacts, second.FinalFacts);
        Assert.Equal(first.StoppedRuleId, second.StoppedRuleId);
        Assert.Equal(first.RuleOutcomes.Select(outcome => outcome.Status), second.RuleOutcomes.Select(outcome => outcome.Status));
        Assert.Equal(first.RuleOutcomes.Select(outcome => outcome.Contractor), second.RuleOutcomes.Select(outcome => outcome.Contractor));
        Assert.Equal(first.RuleOutcomes.Select(outcome => outcome.Description), second.RuleOutcomes.Select(outcome => outcome.Description));
        Assert.Equal(first.RuleOutcomes.Select(outcome => outcome.Labels), second.RuleOutcomes.Select(outcome => outcome.Labels));
    }
}