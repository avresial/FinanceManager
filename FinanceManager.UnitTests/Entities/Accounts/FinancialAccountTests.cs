using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.UnitTests.Entities.Accounts
{
    public class FinancialAccountTests
    {
        private FinancialAccountBase<FinancialEntryBase> FinancialAccount;
        public FinancialAccountTests()
        {
            FinancialAccount = new FinancialAccountBase<FinancialEntryBase>("Test account");
        }

        [Fact]
        public void AddData_AddsNotOrderedData()
        {
            // Arrange
            FinancialAccount.Add(new FinancialEntryBase(1, new DateTime(2000, 1, 29), 20, 10));
            FinancialAccount.Add(new FinancialEntryBase(2, new DateTime(2000, 1, 30), 30, 10));
            FinancialAccount.Add(new FinancialEntryBase(3, new DateTime(2000, 1, 28), 10, 10));

            // Act

            // Assert
            Assert.Equal(30, FinancialAccount.Entries.First().Value);
            Assert.Equal(10, FinancialAccount.Entries.Last().Value);
        }
    }
}
