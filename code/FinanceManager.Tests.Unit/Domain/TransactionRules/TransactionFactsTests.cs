using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Tests.Unit.Domain.TransactionRules;

[Trait("Category", "Unit")]
public class TransactionFactsTests
{
    [Fact]
    public void Constructor_TrimTextAndFilterBlankLabels()
    {
        var facts = new TransactionFacts("  ACME  ", " Invoice ", 7, 12.5m, TransactionDirection.Expense, ["Rent", "   ", " Utilities "]);

        Assert.Equal("ACME", facts.Contractor);
        Assert.Equal("Invoice", facts.Description);
        Assert.Equal(7, facts.AccountId);
        Assert.Equal(12.5m, facts.Amount);
        Assert.Equal(TransactionDirection.Expense, facts.Direction);
        IReadOnlyList<string> expected = ["Rent", " Utilities "];
        Assert.Equal(expected, facts.Labels);
    }

    [Fact]
    public void Constructor_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TransactionFacts("a", "b", 1, -1m, TransactionDirection.Expense, []));
    }

    [Fact]
    public void Equality_ComparesLabelsByContentAndOrder()
    {
        var first = new TransactionFacts("ACME", "Invoice", 1, 10m, TransactionDirection.Expense, ["Rent", "Housing"]);
        var sameContent = new TransactionFacts("ACME", "Invoice", 1, 10m, TransactionDirection.Expense, ["Rent", "Housing"]);
        var reordered = new TransactionFacts("ACME", "Invoice", 1, 10m, TransactionDirection.Expense, ["Housing", "Rent"]);
        var differentLabel = new TransactionFacts("ACME", "Invoice", 1, 10m, TransactionDirection.Expense, ["Rent", "Utilities"]);

        Assert.Equal(first, sameContent);
        Assert.NotEqual(first, reordered);
        Assert.NotEqual(first, differentLabel);
    }

    [Fact]
    public void Equality_DistinguishesByOtherFields()
    {
        var first = new TransactionFacts("ACME", "Invoice", 1, 10m, TransactionDirection.Expense, []);

        Assert.NotEqual(first, new TransactionFacts("ACME", "Other", 1, 10m, TransactionDirection.Expense, []));
        Assert.NotEqual(first, new TransactionFacts("ACME", "Invoice", 2, 10m, TransactionDirection.Expense, []));
        Assert.NotEqual(first, new TransactionFacts("ACME", "Invoice", 1, 11m, TransactionDirection.Expense, []));
        Assert.NotEqual(first, new TransactionFacts("ACME", "Invoice", 1, 10m, TransactionDirection.Income, []));
    }

    [Fact]
    public void Empty_HasNeutralValues()
    {
        var facts = TransactionFacts.Empty;

        Assert.Equal(string.Empty, facts.Contractor);
        Assert.Equal(string.Empty, facts.Description);
        Assert.Equal(0, facts.AccountId);
        Assert.Equal(0m, facts.Amount);
        Assert.Empty(facts.Labels);
    }
}