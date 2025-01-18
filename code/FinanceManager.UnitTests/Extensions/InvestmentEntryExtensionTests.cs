using FinanceManager.Domain.Entities.Accounts;
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
            List<InvestmentEntry> entries = new List<InvestmentEntry>()
            {
                new InvestmentEntry(1,new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock),
                new InvestmentEntry(2,new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock),
                new InvestmentEntry(3,new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock),
                new InvestmentEntry(4,new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock),
            };

            // Act
            var testValue = entries.GetPrevious(new DateTime(2000, 1, 3), "Ticker1").First();

            // Assert
            Assert.Equal(new DateTime(2000, 1, 2), testValue.PostingDate);
        }
        [Fact]
        public async Task GetPrevious_MultipleTickers()
        {
            // Arrange
            List<InvestmentEntry> entries = new List<InvestmentEntry>()
            {
                new InvestmentEntry(1,new DateTime(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock),
                new InvestmentEntry(2,new DateTime(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock),
                new InvestmentEntry(3,new DateTime(2000, 1, 3), 0, 100, "Ticker2", InvestmentType.Stock),
                new InvestmentEntry(4,new DateTime(2000, 1, 2), 0, 100, "Ticker2", InvestmentType.Stock),
                new InvestmentEntry(5,new DateTime(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock),
                new InvestmentEntry(6,new DateTime(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock),
            };

            // Act
            var testValue = entries.GetPrevious(new DateTime(2000, 1, 4), "Ticker1").First();

            // Assert
            Assert.Equal(new DateTime(2000, 1, 3), testValue.PostingDate);
        }
    }
}
