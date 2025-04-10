using FinanceManager.Domain.Entities.Accounts;

namespace FinanceManager.UnitTests.Entities.Accounts
{
    public class FinancialAccountTests
    {
        private FinancialAccountBase<FinancialEntryBase> FinancialAccount;
        public FinancialAccountTests()
        {
            FinancialAccount = new FinancialAccountBase<FinancialEntryBase>(1, 1, "Test account");
        }

        [Fact]
        public void AddData_AddsNotOrderedData()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 29), 20, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 2, new DateTime(2000, 1, 30), 30, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 3, new DateTime(2000, 1, 28), 10, 10));

            // Act

            // Assert
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Equal(30, FinancialAccount.Entries.First().Value);
            Assert.Equal(10, FinancialAccount.Entries.Last().Value);
        }
        [Fact]
        public async Task AddEntries_FromYoungersToOldest_SingleTickers()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 30), 10, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 2, new DateTime(2000, 1, 29), 10, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 3, new DateTime(2000, 1, 28), 10, 10));

            // Act

            // Assert
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Equal(30, FinancialAccount.Entries.First().Value);
            Assert.Equal(10, FinancialAccount.Entries.Last().Value);
        }
        [Fact]
        public void AddSingleEntryWithWrongValue_ValueIsRecalculatedProprely()
        {
            // Arrange
            // Act
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 1), 0, 10));

            // Assert
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Equal(10, FinancialAccount.Get(new DateTime(2000, 1, 1)).First().Value);
            Assert.Equal(10, FinancialAccount.Get(new DateTime(2000, 1, 1)).First().ValueChange);
        }
        [Fact]
        public void UpdateData_ChangeDate()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 29), 30, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 2, new DateTime(2000, 1, 30), 40, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 3, new DateTime(2000, 1, 28), 20, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 4, new DateTime(2000, 1, 26), 10, 10));

            // Act
            var entryToChange = FinancialAccount.Get(new DateTime(2000, 1, 30)).First();
            FinancialEntryBase change = new FinancialEntryBase(entryToChange.AccountId, entryToChange.EntryId, new DateTime(2000, 1, 27), entryToChange.Value, entryToChange.ValueChange);
            FinancialAccount.UpdateEntry(change, true);

            // Assert
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Equal(40, FinancialAccount.Entries.First().Value);
            Assert.Equal(29, FinancialAccount.Entries.First().PostingDate.Day);
            Assert.Equal(10, FinancialAccount.Entries.Last().Value);
        }

        [Fact]
        public void RemoveData_RecalculatesValues()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 29), 40, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 2, new DateTime(2000, 1, 30), 50, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 3, new DateTime(2000, 1, 28), 30, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 4, new DateTime(2000, 1, 27), 20, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 5, new DateTime(2000, 1, 26), 10, 10));

            // Act
            FinancialAccount.Remove(3);

            // Assert
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Equal(40, FinancialAccount.Entries.First().Value);
            Assert.Equal(10, FinancialAccount.Entries.Last().Value);
        }
        [Fact]
        public void RemoveData_RemovesOldestElement_RecalculatesValues()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 1), 10, 10));
            FinancialAccount.Add(new FinancialEntryBase(1, 2, new DateTime(2000, 1, 2), 20, 10));

            // Act
            FinancialAccount.Remove(1);

            // Assert
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Single(FinancialAccount.Entries);
            Assert.Equal(10, FinancialAccount.Entries.First().Value);
            Assert.Equal(10, FinancialAccount.Entries.Last().Value);
        }
        [Fact]
        public void RemoveData_OnlyOneElement_RecalculatesValues()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 28), 30, 10));

            // Act
            FinancialAccount.Remove(1);

            // Assert
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Empty(FinancialAccount.Entries);
        }
        [Fact]
        public void RemoveData_SingleElementGetsDeleted_NoEntriesAreLEft()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, 1, new DateTime(2000, 1, 29), 40, 10));

            // Act
            FinancialAccount.Remove(1);

            // Assert
            Assert.NotNull(FinancialAccount);
            Assert.NotNull(FinancialAccount.Entries);
            Assert.Empty(FinancialAccount.Entries);
        }
    }
}
