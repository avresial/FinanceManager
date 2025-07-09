using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.UnitTests.Extensions
{
    public class InvestmentEntryExtensionTests
    {
        [Fact]
        public async Task GetPrevious()
        {
            // Arrange
            List<StockAccountEntry> entries = new List<StockAccountEntry>()
            {
                new StockAccountEntry(1,1,new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock),
                new StockAccountEntry(1,2,new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock),
                new StockAccountEntry(1,3,new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock),
                new StockAccountEntry(1,4,new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock),
            };

            // Act
            var testValue = entries.GetNextOlder(new DateTime(2000, 1, 3), "Ticker1").First();

            // Assert
            Assert.Equal(new DateTime(2000, 1, 2), testValue.PostingDate);
        }
        [Fact]
        public async Task GetPrevious_MultipleTickers()
        {
            // Arrange
            List<StockAccountEntry> entries = new List<StockAccountEntry>()
            {
                new StockAccountEntry(1,1,new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock),
                new StockAccountEntry(1,2,new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock),
                new StockAccountEntry(1,3,new DateTime(2000, 1, 3), 0, 100, "Ticker2", InvestmentType.Stock),
                new StockAccountEntry(1,4,new DateTime(2000, 1, 2), 0, 100, "Ticker2", InvestmentType.Stock),
                new StockAccountEntry(1,5,new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock),
                new StockAccountEntry(1,6,new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock),
            };

            // Act
            var testValue = entries.GetNextOlder(new DateTime(2000, 1, 4), "Ticker1").First();

            // Assert
            Assert.Equal(new DateTime(2000, 1, 3), testValue.PostingDate);
        }
    }
}
