using FinanceManager.Components.Features.TransactionRules.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

namespace FinanceManager.Tests.Unit.Components.Features.TransactionRules;

[Trait("Category", "Unit")]
public class TransactionRuleAccountLabelsTests
{
    [Fact]
    public void Get_DuplicateNamesWithoutLabels_AreNumbered()
    {
        var labels = new TransactionRuleAccountLabels([new(1, "Main"), new(2, "Main"), new(3, "Savings")], Loaded: true);

        Assert.Equal("Main (1 of 2)", labels.Get(1));
        Assert.Equal("Main (2 of 2)", labels.Get(2));
        Assert.Equal("Savings", labels.Get(3));
    }

    [Fact]
    public void UnknownAccount_IsDeletedOnlyWhenAccountsLoaded()
    {
        var loaded = new TransactionRuleAccountLabels([new AvailableAccount(1, "Main")], Loaded: true);

        Assert.True(loaded.IsMissing(9));
        Assert.Equal("Deleted account (#9)", loaded.Get(9));
        Assert.False(TransactionRuleAccountLabels.NotLoaded.IsMissing(9));
        Assert.Equal("Account #9", TransactionRuleAccountLabels.NotLoaded.Get(9));
    }
}