using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.UnitTests.Entities.Accounts
{
    public class BankAccountTests
    {
        private BankAccount BankAccount;
        public BankAccountTests()
        {
            BankAccount = new BankAccount(1, "Test account", Core.Enums.AccountType.Other);
        }

        [Fact]
        public void UpdateData_ChangeDate()
        {
            // Arrange
            BankAccount.Add(new BankAccountEntry(1, new DateTime(2000, 1, 29), 30, 10));
            BankAccount.Add(new BankAccountEntry(2, new DateTime(2000, 1, 30), 40, 10) { Description = "Test0" });
            BankAccount.Add(new BankAccountEntry(3, new DateTime(2000, 1, 28), 20, 10));
            BankAccount.Add(new BankAccountEntry(4, new DateTime(2000, 1, 26), 10, 10));

            // Act
            var entryToChange = BankAccount.Get(new DateTime(2000, 1, 30)).FirstOrDefault();
            var change = new BankAccountEntry(entryToChange.Id, new DateTime(2000, 1, 27), entryToChange.Value, entryToChange.ValueChange) { Description = "Test1" };
            BankAccount.Update(change, true);

            // Assert
            Assert.Equal(40, BankAccount.Entries.First().Value);
            Assert.Equal(29, BankAccount.Entries.First().PostingDate.Day);
            Assert.Equal(10, BankAccount.Entries.Last().Value);
            Assert.Equal("Test1", BankAccount.Get(new DateTime(2000, 1, 27)).FirstOrDefault().Description);
        }
    }
}
