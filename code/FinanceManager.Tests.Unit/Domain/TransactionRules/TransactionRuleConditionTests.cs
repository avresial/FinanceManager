using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Models;
using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Domain.TransactionRules;

[Trait("Category", "Unit")]
public class TransactionRuleConditionTests
{
    [Theory]
    [InlineData("ACME Corp.", "acme", TextMatchOperator.Contains, true, true)]
    [InlineData("ACME Corp.", "ACME", TextMatchOperator.Contains, false, true)]
    [InlineData("ACME Corp.", "acme", TextMatchOperator.Contains, false, false)]
    [InlineData("ACME Corp.", "ACME Corp.", TextMatchOperator.Equals, false, true)]
    [InlineData("ACME Corp.", "acme corp.", TextMatchOperator.Equals, true, true)]
    [InlineData("ACME Corp.", "ACME Corp. ", TextMatchOperator.Equals, true, true)]
    [InlineData("ACME Corp.", "acme", TextMatchOperator.StartsWith, true, true)]
    [InlineData("ACME Corp.", "corp.", TextMatchOperator.EndsWith, true, true)]
    [InlineData("ACME Corp.", "CORP", TextMatchOperator.EndsWith, false, false)]
    [InlineData("Invoice #123", "^invoice #\\d+$", TextMatchOperator.RegularExpression, true, true)]
    [InlineData("Invoice #123", "^invoice #\\d+$", TextMatchOperator.RegularExpression, false, false)]
    [InlineData("Invoice #123", "^deposit", TextMatchOperator.RegularExpression, true, false)]
    public void ContractorCondition_MatchesAccordingToOperator(string value, string pattern, TextMatchOperator matchOperator, bool ignoreCase, bool expected)
    {
        var condition = new ContractorCondition(pattern, matchOperator, ignoreCase);

        Assert.Equal(expected, condition.Matches(new TransactionFacts(value, "d", 1, 1m, TransactionDirection.Expense, [])));
    }

    [Fact]
    public void ContractorCondition_NullPattern_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ContractorCondition(null!));

    [Fact]
    public void ContractorCondition_BlankPattern_Throws() =>
        Assert.Throws<ArgumentException>(() => new ContractorCondition("   "));

    [Fact]
    public void ContractorCondition_MalformedRegex_Throws() =>
        Assert.Throws<RegexParseException>(() => new ContractorCondition("(unclosed", TextMatchOperator.RegularExpression));

    [Fact]
    public void ContractorCondition_LiteralOperatorsAcceptRegexCharacters()
    {
        var condition = new ContractorCondition("[", TextMatchOperator.Contains);

        Assert.True(condition.Matches(new TransactionFacts("ACME [test]", "d", 1, 1m, TransactionDirection.Expense, [])));
    }

    [Fact]
    public void ContractorCondition_TimedOutRegex_DoesNotBlockRuleEvaluation()
    {
        var condition = new ContractorCondition("^(a+)+$", TextMatchOperator.RegularExpression, false);
        var facts = new TransactionFacts(new string('a', 100_000) + "!", "d", 1, 1m, TransactionDirection.Expense, []);

        Assert.False(condition.Matches(facts));
    }

    [Theory]
    [InlineData("Invoice #123", "invoice", TextMatchOperator.Contains, true, true)]
    [InlineData("Invoice #123", "Invoice", TextMatchOperator.StartsWith, false, true)]
    [InlineData("Invoice #123", "nothing", TextMatchOperator.Contains, true, false)]
    public void DescriptionCondition_MatchesDescriptionText(string value, string pattern, TextMatchOperator matchOperator, bool ignoreCase, bool expected)
    {
        var condition = new DescriptionCondition(pattern, matchOperator, ignoreCase);

        Assert.Equal(expected, condition.Matches(new TransactionFacts("c", value, 1, 1m, TransactionDirection.Expense, [])));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(11, true)]
    [InlineData(12, false)]
    public void AccountCondition_MatchesConfiguredAccountIds(int accountId, bool expected)
    {
        var condition = new AccountCondition([5, 11]);

        Assert.Equal(expected, condition.Matches(new TransactionFacts("c", "d", accountId, 1m, TransactionDirection.Expense, [])));
    }

    [Fact]
    public void AccountCondition_WithoutAccountIds_MatchesNothing()
    {
        var condition = new AccountCondition([]);

        Assert.False(condition.Matches(new TransactionFacts("c", "d", 5, 1m, TransactionDirection.Expense, [])));
    }

    [Theory]
    [InlineData(TransactionDirection.Expense, true)]
    [InlineData(TransactionDirection.Income, false)]
    [InlineData(TransactionDirection.Transfer, false)]
    public void DirectionCondition_MatchesExactDirection(TransactionDirection factsDirection, bool expected)
    {
        var condition = new DirectionCondition(TransactionDirection.Expense);

        Assert.Equal(expected, condition.Matches(new TransactionFacts("c", "d", 1, 1m, factsDirection, [])));
    }

    [Fact]
    public void AmountCondition_RequiresMatchingDirection()
    {
        var condition = new AmountCondition(TransactionDirection.Expense, 50m, AmountComparison.GreaterThan);
        var expense = new TransactionFacts("c", "d", 1, 60m, TransactionDirection.Expense, []);
        var income = new TransactionFacts("c", "d", 1, 60m, TransactionDirection.Income, []);

        Assert.True(condition.Matches(expense));
        Assert.False(condition.Matches(income));
    }
}