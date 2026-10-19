using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Tests.Unit.Domain.Entities.Accounts;

[Collection("Domain")]
[Trait("Category", "Unit")]
public class CurrencyAccountTests
{
    private readonly CurrencyAccount _account = new(1, 1, "Test account", FinanceManager.Domain.FinancialAccounts.Shared.Entities.AccountLabel.Other);

    [Fact]
    public void AddEntry_WithZeroValue_RecalculatesValueCorrectly()
    {
        // Arrange
        // Act
        _account.Add(new CurrencyAccountEntry(1, 1, new(2000, 1, 1), 0, 10));

        // Assert
        Assert.NotNull(_account.Entries);
        Assert.Equal(10, _account.Get(new(2000, 1, 1)).First().Value);
        Assert.Equal(10, _account.Get(new(2000, 1, 1)).First().ValueChange);
    }

    [Fact]
    public void AddSingleEntry_WithNonExistentDate_ReturnsDefaultValues()
    {
        // Arrange
        // Act
        _account.Add(new CurrencyAccountEntry(1, 1, new(2000, 1, 1), 0, 10));

        // Assert
        Assert.NotNull(_account.Entries);
        Assert.Equal(10, _account.Get(new(2001, 1, 1)).First().Value);
        Assert.Equal(10, _account.Get(new(2001, 1, 1)).First().ValueChange);
    }
}