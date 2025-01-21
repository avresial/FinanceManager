using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.UnitTests.Entities.Accounts
{
    public class BankAccountTests
    {
        private BankAccount BankAccount;
        public BankAccountTests()
        {
            BankAccount = new BankAccount(1, 1, "Test account", Domain.Enums.AccountType.Other);
        }

        [Fact]
        public void UpdateData_ChangeDate()
        {
            // Arrange
            BankAccount.Add(new BankAccountEntry(1, 1, new DateTime(2000, 1, 29), 30, 10));
            BankAccount.Add(new BankAccountEntry(1, 2, new DateTime(2000, 1, 30), 40, 10) { Description = "Test0" });
            BankAccount.Add(new BankAccountEntry(1, 3, new DateTime(2000, 1, 28), 20, 10));
            BankAccount.Add(new BankAccountEntry(1, 4, new DateTime(2000, 1, 26), 10, 10));

            // Act
            var entryToChange = BankAccount.Get(new DateTime(2000, 1, 30)).First();
            var change = new BankAccountEntry(entryToChange.AccountId, entryToChange.EntryId, new DateTime(2000, 1, 27), entryToChange.Value, entryToChange.ValueChange)
            { Description = "Test1" };
            BankAccount.UpdateEntry(change, true);

            // Assert
            Assert.NotNull(BankAccount.Entries);
            Assert.Equal(40, BankAccount.Entries.First().Value);
            Assert.Equal(29, BankAccount.Entries.First().PostingDate.Day);
            Assert.Equal(10, BankAccount.Entries.Last().Value);
            Assert.Equal("Test1", BankAccount.Get(new DateTime(2000, 1, 27)).First().Description);
        }
    }
}
