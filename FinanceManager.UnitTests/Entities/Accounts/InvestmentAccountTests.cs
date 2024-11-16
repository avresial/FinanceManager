using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;

namespace FinanceManager.UnitTests.Entities.Accounts
{
    public class InvestmentAccountTests
    {
        private InvestmentAccount investmentAccount;
        public InvestmentAccountTests()
        {
            investmentAccount = new InvestmentAccount("TestAccount");
        }


        [Fact]
        public async Task AddEntries()
        {
            // Arrange
            InvestmentEntry investmentEntry1 = new InvestmentEntry(new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry2 = new InvestmentEntry(new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry3 = new InvestmentEntry(new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry4 = new InvestmentEntry(new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock);
            // Act
            investmentAccount.Add(investmentEntry1);
            investmentAccount.Add(investmentEntry2);
            investmentAccount.Add(investmentEntry3);
            investmentAccount.Add(investmentEntry4);

            // Assert
            Assert.Equal(400, investmentAccount.Entries.First().Value);
        }

    }
}
