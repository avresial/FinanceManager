using FinanceManager.Domain.FinancialAccounts.Shared.Extensions;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Extensions;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Tests.Unit.Domain.Extensions;

[Collection("Domain")]
[Trait("Category", "Unit")]
public class InvestmentEntryExtensionTests
{
    [Fact]
    public void GetPrevious()
    {
        // Arrange
        List<StockAccountEntry> entries =
        [
            new (1,1,new (2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock),
            new (1,2,new (2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock),
            new (1,3,new (2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock),
            new (1,4,new (2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock),
        ];

        // Act
        var testValue = entries.GetNextOlder(new DateTime(2000, 1, 3), "Ticker1").First();

        // Assert
        Assert.Equal(new DateTime(2000, 1, 2), testValue.PostingDate);
    }
    [Fact]
    public void GetPrevious_MultipleTickers()
    {
        // Arrange
        List<StockAccountEntry> entries =
        [
            new (1,1,new (2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock),
            new (1,2,new (2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock),
            new (1,3,new (2000, 1, 3), 0, 100, "Ticker2", InvestmentType.Stock),
            new (1,4,new (2000, 1, 2), 0, 100, "Ticker2", InvestmentType.Stock),
            new (1,5,new (2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock),
            new (1,6,new (2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock),
        ];

        // Act
        var testValue = entries.GetNextOlder(new(2000, 1, 4), "Ticker1").First();

        // Assert
        Assert.Equal(new(2000, 1, 3), testValue.PostingDate);
    }
}