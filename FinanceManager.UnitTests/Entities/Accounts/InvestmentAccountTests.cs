using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Extensions;

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
        public async Task AddEntries_SingleTicker()
        {
            // Arrange
            InvestmentEntry investmentEntry1 = new InvestmentEntry(1, new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry2 = new InvestmentEntry(2, new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry3 = new InvestmentEntry(3, new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry4 = new InvestmentEntry(4, new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock);
            // Act
            investmentAccount.Add(investmentEntry1);
            investmentAccount.Add(investmentEntry2);
            investmentAccount.Add(investmentEntry3);
            investmentAccount.Add(investmentEntry4);

            // Assert
            Assert.Equal(400, investmentAccount.Entries.First().Value);
        }

        [Fact]
        public async Task AddEntries_MultipleTickers()
        {
            // Arrange
            InvestmentEntry investmentEntry1 = new InvestmentEntry(1, new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry2 = new InvestmentEntry(2, new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntryy2 = new InvestmentEntry(3, new DateTime(2000, 1, 3), 0, 99, "Ticker2", InvestmentType.Stock);
            InvestmentEntry investmentEntry3 = new InvestmentEntry(4, new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);
            InvestmentEntry investmentEntry4 = new InvestmentEntry(5, new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock);
            // Act
            investmentAccount.Add(investmentEntry1);
            investmentAccount.Add(investmentEntry2);
            investmentAccount.Add(investmentEntryy2);
            investmentAccount.Add(investmentEntry3);
            investmentAccount.Add(investmentEntry4);

            // Assert
            IEnumerable<InvestmentEntry> resultValues = investmentAccount.Get(new DateTime(2000, 1, 4));
            Assert.Equal(400, resultValues.Get(new DateTime(2000, 1, 4)).FirstOrDefault().Value);
        }

    }
}
