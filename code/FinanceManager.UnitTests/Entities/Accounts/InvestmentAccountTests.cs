using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.UnitTests.Entities.Accounts
{
    public class InvestmentAccountTests
    {
        private StockAccount investmentAccount;
        public InvestmentAccountTests()
        {
            investmentAccount = new StockAccount(1, 1, "TestAccount");
        }


        [Fact]
        public void AddEntries_SingleTicker()
        {
            // Arrange
            StockAccountEntry investmentEntry1 = new StockAccountEntry(1, 1, new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry2 = new StockAccountEntry(1, 2, new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry3 = new StockAccountEntry(1, 3, new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry4 = new StockAccountEntry(1, 4, new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock);

            // Act
            investmentAccount.Add(investmentEntry1);
            investmentAccount.Add(investmentEntry2);
            investmentAccount.Add(investmentEntry3);
            investmentAccount.Add(investmentEntry4);

            // Assert
            Assert.NotNull(investmentAccount.Entries);
            Assert.Equal(400, investmentAccount.Entries.First().Value);
        }

        [Fact]
        public void AddEntries_MultipleTickers()
        {
            // Arrange
            StockAccountEntry investmentEntry1 = new StockAccountEntry(1, 1, new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry2 = new StockAccountEntry(1, 2, new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntryy2 = new StockAccountEntry(1, 3, new DateTime(2000, 1, 3), 0, 99, "Ticker2", InvestmentType.Stock);
            StockAccountEntry investmentEntry3 = new StockAccountEntry(1, 4, new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry4 = new StockAccountEntry(1, 5, new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock);
            // Act
            investmentAccount.Add(investmentEntry1);
            investmentAccount.Add(investmentEntry2);
            investmentAccount.Add(investmentEntryy2);
            investmentAccount.Add(investmentEntry3);
            investmentAccount.Add(investmentEntry4);

            // Assert
            IEnumerable<StockAccountEntry> resultValues = investmentAccount.Get(new DateTime(2000, 1, 4));
            Assert.Equal(400, resultValues.Get(new DateTime(2000, 1, 4)).First().Value);
        }

        [Fact]
        public void AddEntries_FromYoungersToOldest_SingleTickers()
        {
            // Arrange
            StockAccountEntry investmentEntry1 = new StockAccountEntry(1, 1, new DateTime(2000, 1, 3), 100, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry2 = new StockAccountEntry(1, 2, new DateTime(2000, 1, 2), 100, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry3 = new StockAccountEntry(1, 3, new DateTime(2000, 1, 1), 100, 100, "Ticker1", InvestmentType.Stock);

            // Act
            investmentAccount.Add(investmentEntry1);
            investmentAccount.Add(investmentEntry2);
            investmentAccount.Add(investmentEntry3);

            // Assert
            IEnumerable<StockAccountEntry> resultValues = investmentAccount.Get(new DateTime(2000, 1, 4));
            Assert.Equal(300, resultValues.Get(new DateTime(2000, 1, 4)).First().Value);
        }

        [Fact]
        public void UpdateEntries_SingleTicker()
        {
            // Arrange
            StockAccountEntry investmentEntry0 = new StockAccountEntry(1, 2, new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
            StockAccountEntry investmentEntry1 = new StockAccountEntry(1, 3, new DateTime(2000, 1, 3), 0, 99, "TickerToUpdate", InvestmentType.Stock);
            StockAccountEntry investmentEntry2 = new StockAccountEntry(1, 4, new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);

            investmentAccount.Add(investmentEntry0);
            investmentAccount.Add(investmentEntry1);
            investmentAccount.Add(investmentEntry2);

            // Act
            var test = investmentAccount.Get(new DateTime(2000, 1, 3)).First(x => x.Ticker == "TickerToUpdate");
            test.Update(new StockAccountEntry(1, 3, new DateTime(2000, 1, 3), 0, 50, "TickerToUpdate_TickerChanged", InvestmentType.Stock));

            // Assert
            var updateResult = investmentAccount.Get(new DateTime(2000, 1, 3)).First(x => x.Ticker == "TickerToUpdate_TickerChanged");
            Assert.Equal(50, updateResult.ValueChange);
        }

        [Fact]
        public void RemoveData_RemovesOldestElement_RecalculatesValues()
        {
            // Arrange
            investmentAccount.Add(new StockAccountEntry(1, 1, new DateTime(2000, 1, 3), 10, 10, "Ticker1", InvestmentType.Stock));
            investmentAccount.Add(new StockAccountEntry(1, 2, new DateTime(2000, 1, 4), 20, 10, "Ticker1", InvestmentType.Stock));

            // Act
            investmentAccount.Remove(1);

            // Assert
            Assert.NotNull(investmentAccount.Entries);
            Assert.Single(investmentAccount.Entries);
            Assert.Equal(10, investmentAccount.Entries.First().Value);
            Assert.Equal(10, investmentAccount.Entries.Last().Value);
        }

    }
}
