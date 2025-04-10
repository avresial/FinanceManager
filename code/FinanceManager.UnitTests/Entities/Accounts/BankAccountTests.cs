using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.UnitTests.Entities.Accounts;

public class BankAccountTests
{
    private BankAccount _bankAccount;
    public BankAccountTests()
    {
        _bankAccount = new BankAccount(1, 1, "Test account", Domain.Enums.AccountType.Other);
    }

    [Fact]
    public void UpdateData_ChangeDate()
    {
        // Arrange
        _bankAccount.Add(new BankAccountEntry(1, 1, new DateTime(2000, 1, 29), 30, 10));
        _bankAccount.Add(new BankAccountEntry(1, 2, new DateTime(2000, 1, 30), 40, 10) { Description = "Test0" });
        _bankAccount.Add(new BankAccountEntry(1, 3, new DateTime(2000, 1, 28), 20, 10));
        _bankAccount.Add(new BankAccountEntry(1, 4, new DateTime(2000, 1, 26), 10, 10));

        // Act
        var entryToChange = _bankAccount.Get(new DateTime(2000, 1, 30)).First();
        var change = new BankAccountEntry(entryToChange.AccountId, entryToChange.EntryId, new DateTime(2000, 1, 27), entryToChange.Value, entryToChange.ValueChange)
        { Description = "Test1" };
        _bankAccount.UpdateEntry(change, true);

        // Assert
        Assert.NotNull(_bankAccount.Entries);
        Assert.Equal(40, _bankAccount.Entries.First().Value);
        Assert.Equal(29, _bankAccount.Entries.First().PostingDate.Day);
        Assert.Equal(10, _bankAccount.Entries.Last().Value);
        Assert.Equal("Test1", _bankAccount.Get(new DateTime(2000, 1, 27)).First().Description);
    }

    [Fact]
    public void AddSingleEntryWithWrongValue_ValueIsRecalculatedProprely()
    {
        // Arrange
        // Act
        _bankAccount.Add(new BankAccountEntry(1, 1, new DateTime(2000, 1, 1), 0, 10));

        // Assert
        Assert.NotNull(_bankAccount.Entries);
        Assert.Equal(10, _bankAccount.Get(new DateTime(2000, 1, 1)).First().Value);
        Assert.Equal(10, _bankAccount.Get(new DateTime(2000, 1, 1)).First().ValueChange);
    }
}
