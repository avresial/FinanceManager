using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Enums;

namespace FinanceManager.UnitTests.Domain.Entities.Accounts;

[Collection("Domain")]
[Trait("Category", "Unit")]
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

    [Fact]
    public void RecalculateValues_LargeDataset_ShouldMaintainCorrectness()
    {
        // Arrange - Add 1000 entries
        var baseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Large Dataset Test");

        for (int i = 0; i < 1000; i++)
        {
            account.Add(new FinancialEntryBase(1, i + 1, baseDate.AddDays(i), 0, 10m), recalculateValues: false);
        }

        // Act - Recalculate all at once
        var startTime = DateTime.UtcNow;
        account.RecalculateEntryValues(account.Entries.Count - 1);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.Equal(1000, account.Entries.Count);

        // First entry (youngest/newest) should have highest value
        Assert.Equal(10000m, account.Entries.First().Value);

        // Last entry (oldest) should have lowest value
        Assert.Equal(10m, account.Entries.Last().Value);

        // Verify each entry has correct cumulative value
        for (int i = 0; i < account.Entries.Count; i++)
        {
            var expectedValue = 10m * (1000 - i);
            Assert.Equal(expectedValue, account.Entries[i].Value);
        }

        // Performance check - should complete in under 1 second
        Assert.True(duration.TotalSeconds < 1, $"Recalculation took {duration.TotalSeconds}s, expected < 1s");
    }

    [Fact]
    public void Add_WithRecalculateFalse_ThenExplicitRecalculate_ShouldCorrectValues()
    {
        // Arrange
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Test");

        // Add multiple entries without recalculation
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 0, 100m), recalculateValues: false);
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 2), 0, 50m), recalculateValues: false);
        account.Add(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 3), 0, 25m), recalculateValues: false);

        // Act - Values should be incorrect initially
        var valuesBeforeRecalc = account.Entries.Select(e => e.Value).ToList();

        // Explicit recalculation
        account.RecalculateEntryValues(account.Entries.Count - 1);

        // Assert
        Assert.Equal(175m, account.Entries.First().Value); // 100 + 50 + 25
        Assert.Equal(150m, account.Entries[1].Value); // 100 + 50
        Assert.Equal(100m, account.Entries.Last().Value); // 100
    }

    [Fact]
    public void Remove_FirstEntry_ShouldRecalculateCorrectly()
    {
        // Arrange - Entry at index 0 (youngest)
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Test");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 30, 30m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 2), 20, -10m));
        account.Add(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 3), 10, -10m));

        // Act - Remove the first entry (youngest) from the list
        account.Remove(account.Entries.First().EntryId);

        // Assert - After removing youngest (entry 3), entry 2 becomes youngest
        Assert.Equal(2, account.Entries.Count);
        Assert.Equal(20m, account.Entries.First().Value); // Entry 2 (now youngest): 30 + (-10) = 20
        Assert.Equal(30m, account.Entries.Last().Value); // Entry 1 (oldest): 30
    }

    [Fact]
    public void UpdateEntry_LastEntry_ShouldUpdateCorrectly()
    {
        // Arrange
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Test");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 100, 100m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 2), 50, -50m));

        // Act - Update last (newest) entry
        var updatedEntry = new FinancialEntryBase(1, 2, new DateTime(2023, 1, 2), 0, -30m);
        account.UpdateEntry(updatedEntry);

        // Assert
        Assert.Equal(2, account.Entries.Count);
        Assert.Equal(70m, account.Entries.First().Value); // 100 + (-30)
        Assert.Equal(100m, account.Entries.Last().Value);
    }

    [Fact]
    public void InterleavedOperations_ShouldMaintainCorrectValues()
    {
        // Arrange & Act - Mix of Add, Update, Remove
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Test");

        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 0, 100m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 3), 0, 50m));
        account.Add(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 5), 0, 25m));

        // Insert in middle
        account.Add(new FinancialEntryBase(1, 4, new DateTime(2023, 1, 2), 0, 30m));

        // Update middle entry
        account.UpdateEntry(new FinancialEntryBase(1, 4, new DateTime(2023, 1, 2), 0, 40m));

        // Remove middle entry
        account.Remove(2);

        // Add at beginning
        account.Add(new FinancialEntryBase(1, 5, new DateTime(2022, 12, 31), 0, 10m));

        // Assert - Final state should be correct
        Assert.Equal(4, account.Entries.Count);

        // Chronological order: 2022-12-31, 2023-01-01, 2023-01-02, 2023-01-05
        var sortedEntries = account.Entries.OrderBy(e => e.PostingDate).ToList();

        Assert.Equal(10m, sortedEntries[0].Value); // 10
        Assert.Equal(110m, sortedEntries[1].Value); // 10 + 100 
        Assert.Equal(150m, sortedEntries[2].Value); // 10 + 100 + 40
        Assert.Equal(175m, sortedEntries[3].Value); // 10 + 100 + 40 + 25
    }

    [Fact]
    public void BoundaryCondition_EmptyAccount_RecalculateShouldNotThrow()
    {
        // Arrange
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Empty");

        // Act & Assert
        account.RecalculateEntryValues(0); // Should not throw
        account.RecalculateEntryValues(null); // Should not throw
        Assert.Empty(account.Entries);
    }

    [Fact]
    public void BoundaryCondition_SingleEntry_ShouldEqualValueChange()
    {
        // Arrange & Act
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Single");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 0, 42m));

        // Assert
        Assert.Single(account.Entries);
        Assert.Equal(42m, account.Entries.First().Value);
        Assert.Equal(42m, account.Entries.First().ValueChange);
    }

    [Fact]
    public void BoundaryCondition_TwoEntries_ShouldCalculateCorrectly()
    {
        // Arrange & Act
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Two Entries");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 0, 100m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 2), 0, -20m));

        // Assert
        Assert.Equal(2, account.Entries.Count);
        Assert.Equal(100m, account.Entries.Last().Value);
        Assert.Equal(80m, account.Entries.First().Value);// 100 + (-20)
    }

    [Fact]
    public void Add_DuplicateEntryId_ShouldNotAdd()
    {
        // Arrange
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Duplicate Test");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 0, 100m));

        // Act - Try to add entry with same ID
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 2), 0, 50m));

        // Assert - Should only have one entry
        Assert.Single(account.Entries);
        Assert.Equal(100m, account.Entries.First().Value);
    }

    [Fact]
    public void Add_OutOfOrderDates_ShouldMaintainChronologicalOrder()
    {
        // Arrange & Act
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Out of Order");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 15), 0, 100m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 5), 0, 50m));
        account.Add(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 10), 0, 25m));
        account.Add(new FinancialEntryBase(1, 4, new DateTime(2023, 1, 1), 0, 10m));

        // Assert - Should be in chronological order (oldest first in list)
        var dates = account.Entries.Select(e => e.PostingDate).ToList();
        Assert.Equal(new DateTime(2023, 1, 1), dates[3]);
        Assert.Equal(new DateTime(2023, 1, 5), dates[2]);
        Assert.Equal(new DateTime(2023, 1, 10), dates[1]);
        Assert.Equal(new DateTime(2023, 1, 15), dates[0]);

        // Values should accumulate correctly
        Assert.Equal(10m, account.Entries[3].Value); // 10 + 50 + 25 + 100
        Assert.Equal(60m, account.Entries[2].Value); // 10 + 50
        Assert.Equal(85m, account.Entries[1].Value); // 25 + 50 + 25
        Assert.Equal(185m, account.Entries[0].Value); // 25 + 50 + 25 + 100
    }

    [Fact]
    public void Remove_MiddleEntry_ShouldAdjustValuesCorrectly()
    {
        // Arrange
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Remove Middle");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 0, 100m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 2), 0, 50m)); // REMOVED
        account.Add(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 3), 0, 25m));
        account.Add(new FinancialEntryBase(1, 4, new DateTime(2023, 1, 4), 0, 10m));

        // Act - Remove middle entry (id 2)
        account.Remove(2);

        // Assert
        Assert.Equal(3, account.Entries.Count);

        // Values should be recalculated without the removed entry's contribution
        Assert.Equal(135m, account.Entries[0].Value); // 100 + 25 + 10
        Assert.Equal(125m, account.Entries[1].Value); // 100 + 25
        Assert.Equal(100m, account.Entries[2].Value); // 100

        // Verify removed entry is actually gone
        Assert.DoesNotContain(account.Entries, e => e.EntryId == 2);
    }

    [Fact]
    public void UpdateEntry_ChangeDateToBeforeAllOthers_ShouldReorderAndRecalculate()
    {
        // Arrange
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Reorder Test");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 5), 0, 100m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 10), 0, 50m));
        account.Add(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 15), 0, 25m));

        // Act - Update entry 3 to be before all others
        account.UpdateEntry(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 1), 0, 25m));

        // Assert
        var orderedEntries = account.Entries.OrderByDescending(e => e.PostingDate).ToList();

        // Entry 3 should now be last chronologically
        Assert.Equal(3, orderedEntries[2].EntryId);
        Assert.Equal(new DateTime(2023, 1, 1), orderedEntries[2].PostingDate);

        // Values should be recalculated
        Assert.Equal(175m, orderedEntries[0].Value); // 25 + 100 + 50
        Assert.Equal(125m, orderedEntries[1].Value); // 25 + 100 
        Assert.Equal(25m, orderedEntries[2].Value); // 25 
    }

    [Fact]
    public void RecalculateValues_WithNegativeValues_ShouldHandleCorrectly()
    {
        // Arrange
        var account = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Negative Test");
        account.Add(new FinancialEntryBase(1, 1, new DateTime(2023, 1, 1), 0, 1000m));
        account.Add(new FinancialEntryBase(1, 2, new DateTime(2023, 1, 2), 0, -1500m));
        account.Add(new FinancialEntryBase(1, 3, new DateTime(2023, 1, 3), 0, 200m));

        // Assert
        Assert.Equal(-300m, account.Entries[0].Value); // 1000 -1500 + 200
        Assert.Equal(-500m, account.Entries[1].Value); // 1000 - 1500 
        Assert.Equal(1000m, account.Entries[2].Value); // 1000 
    }

    [Theory]
    [InlineData("CurrencyAccount")]
    [InlineData("StockAccount")]
    [InlineData("BondAccount")]
    public void AllAccountTypes_BasicRecalculation_ShouldWorkCorrectly(string accountType)
    {
        // Arrange
        dynamic account = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccount(1, 1, "Test", AccountLabel.Other),
            "StockAccount" => new StockAccount(1, 1, "Test"),
            "BondAccount" => new BondAccount(1, 1, "Test", AccountLabel.Other),
            _ => throw new ArgumentException("Unknown account type")
        };

        dynamic entry1 = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccountEntry(1, 1, new DateTime(2023, 1, 1), 0, 100m) { Labels = [] },
            "StockAccount" => new StockAccountEntry(1, 1, new DateTime(2023, 1, 1), 0, 100m, "AAPL", InvestmentType.Stock),
            "BondAccount" => new BondAccountEntry(1, 1, new DateTime(2023, 1, 1), 0, 100m, 1),
            _ => throw new ArgumentException("Unknown account type")
        };

        dynamic entry2 = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 50m) { Labels = [] },
            "StockAccount" => new StockAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 50m, "AAPL", InvestmentType.Stock),
            "BondAccount" => new BondAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 50m, 1),
            _ => throw new ArgumentException("Unknown account type")
        };

        // Act
        account.Add(entry1, true);
        account.Add(entry2, true);

        // Assert - Base class behavior should work for all derived types
        var entries = (IReadOnlyList<FinancialEntryBase>)account.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(150m, entries.First().Value);
        Assert.Equal(100m, entries.Last().Value);
    }

    [Theory]
    [InlineData("CurrencyAccount")]
    [InlineData("StockAccount")]
    [InlineData("BondAccount")]
    public void AllAccountTypes_RemoveAndRecalculate_ShouldWorkCorrectly(string accountType)
    {
        // Arrange
        dynamic account = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccount(1, 1, "Test", AccountLabel.Other),
            "StockAccount" => new StockAccount(1, 1, "Test"),
            "BondAccount" => new BondAccount(1, 1, "Test", AccountLabel.Other),
            _ => throw new ArgumentException("Unknown account type")
        };

        // Add three entries
        for (int i = 1; i <= 3; i++)
        {
            dynamic entry = accountType switch
            {
                "CurrencyAccount" => new CurrencyAccountEntry(1, i, new DateTime(2023, 1, i), 0, 100m) { Labels = [] },
                "StockAccount" => new StockAccountEntry(1, i, new DateTime(2023, 1, i), 0, 100m, "AAPL", InvestmentType.Stock),
                "BondAccount" => new BondAccountEntry(1, i, new DateTime(2023, 1, i), 0, 100m, 1),
                _ => throw new ArgumentException("Unknown account type")
            };
            account.Add(entry);
        }

        // Act - Remove middle entry
        account.Remove(2);

        // Assert
        var entries = (IReadOnlyList<FinancialEntryBase>)account.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(200m, entries.First().Value); // 100 + 100
        Assert.Equal(100m, entries.Last().Value);
    }

    [Theory]
    [InlineData("CurrencyAccount")]
    [InlineData("StockAccount")]
    [InlineData("BondAccount")]
    public void AllAccountTypes_UpdateAndRecalculate_ShouldWorkCorrectly(string accountType)
    {
        // Arrange
        dynamic account = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccount(1, 1, "Test", AccountLabel.Other),
            "StockAccount" => new StockAccount(1, 1, "Test"),
            "BondAccount" => new BondAccount(1, 1, "Test", AccountLabel.Other),
            _ => throw new ArgumentException("Unknown account type")
        };

        dynamic entry1 = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccountEntry(1, 1, new DateTime(2023, 1, 1), 0, 100m) { Labels = [] },
            "StockAccount" => new StockAccountEntry(1, 1, new DateTime(2023, 1, 1), 0, 100m, "AAPL", InvestmentType.Stock),
            "BondAccount" => new BondAccountEntry(1, 1, new DateTime(2023, 1, 1), 0, 100m, 1),
            _ => throw new ArgumentException("Unknown account type")
        };

        dynamic entry2 = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 50m) { Labels = [] },
            "StockAccount" => new StockAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 50m, "AAPL", InvestmentType.Stock),
            "BondAccount" => new BondAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 50m, 1),
            _ => throw new ArgumentException("Unknown account type")
        };

        account.Add(entry1);
        account.Add(entry2);

        // Act - Update second entry to have different value change
        dynamic updatedEntry = accountType switch
        {
            "CurrencyAccount" => new CurrencyAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 75m) { Labels = [] },
            "StockAccount" => new StockAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 75m, "AAPL", InvestmentType.Stock),
            "BondAccount" => new BondAccountEntry(1, 2, new DateTime(2023, 1, 2), 0, 75m, 1),
            _ => throw new ArgumentException("Unknown account type")
        };

        account.UpdateEntry(updatedEntry);

        // Assert
        var entries = (IReadOnlyList<FinancialEntryBase>)account.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(175m, entries.First().Value); // 100 + 75
        Assert.Equal(100m, entries.Last().Value);
    }
}
