using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.UnitTests.Domain.Entities.Accounts;

[Collection("unit")]
[Trait("Category", "Unit")]
public class CurrencyAccountTests
{
    private readonly CurrencyAccount _account = new(1, 1, "Test account", FinanceManager.Domain.Enums.AccountLabel.Other);

    [Fact]
    public void UpdateEntry_ChangesPostingDateAndDescription()
    {
        // Arrange
        _account.Add(new CurrencyAccountEntry(1, 1, new(2000, 1, 29), 30, 10));
        _account.Add(new CurrencyAccountEntry(1, 2, new(2000, 1, 30), 40, 10) { Description = "Test0" });
        _account.Add(new CurrencyAccountEntry(1, 3, new(2000, 1, 28), 20, 10));
        _account.Add(new CurrencyAccountEntry(1, 4, new(2000, 1, 26), 10, 10));

        // Act
        var entryToChange = _account.Get(new(2000, 1, 30)).First();
        CurrencyAccountEntry change = new(entryToChange.AccountId, entryToChange.EntryId, new(2000, 1, 27), entryToChange.Value, entryToChange.ValueChange)
        { Description = "Test1" };
        _account.UpdateEntry(change, true);

        // Assert
        Assert.NotNull(_account.Entries);
        Assert.Equal(40, _account.Entries.First().Value);
        Assert.Equal(29, _account.Entries.First().PostingDate.Day);
        Assert.Equal(10, _account.Entries.Last().Value);
        Assert.Equal("Test1", _account.Get(new DateTime(2000, 1, 27)).First().Description);
    }

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