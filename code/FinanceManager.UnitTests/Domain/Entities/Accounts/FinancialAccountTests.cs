using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.UnitTests.Domain.Entities.Accounts;

public class FinancialAccountTests
{
    private readonly FinancialAccountBase<FinancialEntryBase> _financialAccount = new(1, 1, "Test account");

    [Fact]
    public void AddData_AddsNotOrderedData()
    {
        // Arrange
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 29), 20, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 2, new(2000, 1, 30), 30, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 3, new(2000, 1, 28), 10, 10));

        // Act

        // Assert
        Assert.NotNull(_financialAccount.Entries);
        Assert.Equal(30, _financialAccount.Entries.First().Value);
        Assert.Equal(10, _financialAccount.Entries.Last().Value);
    }
    [Fact]
    public void AddEntries_FromYoungersToOldest_SingleTickers()
    {
        // Arrange
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 30), 10, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 2, new(2000, 1, 29), 10, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 3, new(2000, 1, 28), 10, 10));

        // Act

        // Assert
        Assert.NotNull(_financialAccount.Entries);
        Assert.Equal(30, _financialAccount.Entries.First().Value);
        Assert.Equal(10, _financialAccount.Entries.Last().Value);
    }
    [Fact]
    public void AddSingleEntryWithWrongValue_ValueIsRecalculatedProprely()
    {
        // Arrange
        // Act
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 1), 0, 10));

        // Assert
        Assert.NotNull(_financialAccount.Entries);
        Assert.Equal(10, _financialAccount.Get(new(2000, 1, 1)).First().Value);
        Assert.Equal(10, _financialAccount.Get(new(2000, 1, 1)).First().ValueChange);
    }
    [Fact]
    public void UpdateData_ChangeDate()
    {
        // Arrange
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 29), 30, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 2, new(2000, 1, 30), 40, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 3, new(2000, 1, 28), 20, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 4, new(2000, 1, 26), 10, 10));

        // Act
        var entryToChange = _financialAccount.Get(new(2000, 1, 30)).First();
        FinancialEntryBase change = new(entryToChange.AccountId, entryToChange.EntryId, new(2000, 1, 27), entryToChange.Value, entryToChange.ValueChange);
        _financialAccount.UpdateEntry(change, true);

        // Assert
        Assert.NotNull(_financialAccount.Entries);
        Assert.Equal(40, _financialAccount.Entries.First().Value);
        Assert.Equal(29, _financialAccount.Entries.First().PostingDate.Day);
        Assert.Equal(10, _financialAccount.Entries.Last().Value);
    }

    [Fact]
    public void RemoveData_RecalculatesValues()
    {
        // Arrange
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 29), 40, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 2, new(2000, 1, 30), 50, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 3, new(2000, 1, 28), 30, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 4, new(2000, 1, 27), 20, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 5, new(2000, 1, 26), 10, 10));

        // Act
        _financialAccount.Remove(3);

        // Assert
        Assert.NotNull(_financialAccount.Entries);
        Assert.Equal(40, _financialAccount.Entries.First().Value);
        Assert.Equal(10, _financialAccount.Entries.Last().Value);
    }
    [Fact]
    public void RemoveData_RemovesOldestElement_RecalculatesValues()
    {
        // Arrange
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 1), 10, 10));
        _financialAccount.Add(new FinancialEntryBase(1, 2, new(2000, 1, 2), 20, 10));

        // Act
        _financialAccount.Remove(1);

        // Assert
        Assert.NotNull(_financialAccount.Entries);
        Assert.Single(_financialAccount.Entries);
        Assert.Equal(10, _financialAccount.Entries.First().Value);
        Assert.Equal(10, _financialAccount.Entries.Last().Value);
    }
    [Fact]
    public void RemoveData_OnlyOneElement_RecalculatesValues()
    {
        // Arrange
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 28), 30, 10));

        // Act
        _financialAccount.Remove(1);

        // Assert
        Assert.NotNull(_financialAccount.Entries);
        Assert.Empty(_financialAccount.Entries);
    }
    [Fact]
    public void RemoveData_SingleElementGetsDeleted_NoEntriesAreLEft()
    {
        // Arrange
        _financialAccount.Add(new FinancialEntryBase(1, 1, new(2000, 1, 29), 40, 10));

        // Act
        _financialAccount.Remove(1);

        // Assert
        Assert.NotNull(_financialAccount);
        Assert.NotNull(_financialAccount.Entries);
        Assert.Empty(_financialAccount.Entries);
    }
}