using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.UnitTests.Domain.Entities.Accounts;

[Collection("unit")]
[Trait("Category", "Unit")]
public class InvestmentAccountTests
{
    private readonly StockAccount _investmentAccount = new(1, 1, "TestAccount");

    [Fact]
    public void AddEntries_SingleTicker()
    {
        // Arrange
        StockAccountEntry investmentEntry1 = new(1, 1, new(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry2 = new(1, 2, new(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry3 = new(1, 3, new(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry4 = new(1, 4, new(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock);

        // Act
        _investmentAccount.Add(investmentEntry1);
        _investmentAccount.Add(investmentEntry2);
        _investmentAccount.Add(investmentEntry3);
        _investmentAccount.Add(investmentEntry4);

        // Assert
        Assert.NotNull(_investmentAccount.Entries);
        Assert.Equal(400, _investmentAccount.Entries.First().Value);
    }
    [Fact]
    public void AddSingleEntryWithWrongValue_ValueIsRecalculatedProprely()
    {
        // Arrange
        // Act
        _investmentAccount.Add(new StockAccountEntry(1, 1, new DateTime(2000, 1, 1), 0, 10, "Ticker1", InvestmentType.Stock));

        // Assert
        Assert.NotNull(_investmentAccount.Entries);
        Assert.Equal(10, _investmentAccount.Get(new DateTime(2000, 1, 1)).First().Value);
        Assert.Equal(10, _investmentAccount.Get(new DateTime(2000, 1, 1)).First().ValueChange);
    }
    [Fact]
    public void AddEntries_MultipleTickers()
    {
        // Arrange
        StockAccountEntry investmentEntry1 = new(1, 1, new(2000, 1, 1), 0, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry2 = new(1, 2, new(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntryy2 = new(1, 3, new(2000, 1, 3), 0, 99, "Ticker2", InvestmentType.Stock);
        StockAccountEntry investmentEntry3 = new(1, 4, new(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry4 = new(1, 5, new(2000, 1, 4), 0, 100, "Ticker1", InvestmentType.Stock);
        // Act
        _investmentAccount.Add(investmentEntry1);
        _investmentAccount.Add(investmentEntry2);
        _investmentAccount.Add(investmentEntryy2);
        _investmentAccount.Add(investmentEntry3);
        _investmentAccount.Add(investmentEntry4);

        // Assert
        IEnumerable<StockAccountEntry> resultValues = _investmentAccount.Get(new DateTime(2000, 1, 4));
        Assert.Equal(400, resultValues.Get(new DateTime(2000, 1, 4)).First().Value);
    }

    [Fact]
    public void AddEntries_FromYoungersToOldest_SingleTickers()
    {
        // Arrange
        StockAccountEntry investmentEntry1 = new(1, 1, new(2000, 1, 3), 100, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry2 = new(1, 2, new(2000, 1, 2), 100, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry3 = new(1, 3, new(2000, 1, 1), 100, 100, "Ticker1", InvestmentType.Stock);

        // Act
        _investmentAccount.Add(investmentEntry1);
        _investmentAccount.Add(investmentEntry2);
        _investmentAccount.Add(investmentEntry3);

        // Assert
        IEnumerable<StockAccountEntry> resultValues = _investmentAccount.Get(new DateTime(2000, 1, 4));
        Assert.Equal(300, resultValues.Get(new DateTime(2000, 1, 4)).First().Value);
    }

    [Fact]
    public void UpdateEntries_SingleTicker()
    {
        // Arrange
        StockAccountEntry investmentEntry0 = new(1, 2, new(2000, 1, 3), 0, 100, "Ticker1", InvestmentType.Stock);
        StockAccountEntry investmentEntry1 = new(1, 3, new(2000, 1, 3), 0, 99, "TickerToUpdate", InvestmentType.Stock);
        StockAccountEntry investmentEntry2 = new(1, 4, new(2000, 1, 2), 0, 100, "Ticker1", InvestmentType.Stock);

        _investmentAccount.Add(investmentEntry0);
        _investmentAccount.Add(investmentEntry1);
        _investmentAccount.Add(investmentEntry2);

        // Act
        var test = _investmentAccount.Get(new(2000, 1, 3)).First(x => x.Ticker == "TickerToUpdate");
        test.Update(new(1, 3, new(2000, 1, 3), 0, 50, "TickerToUpdate_TickerChanged", InvestmentType.Stock));

        // Assert
        var updateResult = _investmentAccount.Get(new(2000, 1, 3)).First(x => x.Ticker == "TickerToUpdate_TickerChanged");
        Assert.Equal(50, updateResult.ValueChange);
    }

    [Fact]
    public void RemoveData_RemovesOldestElement_RecalculatesValues()
    {
        // Arrange
        _investmentAccount.Add(new StockAccountEntry(1, 1, new(2000, 1, 3), 10, 10, "Ticker1", InvestmentType.Stock));
        _investmentAccount.Add(new StockAccountEntry(1, 2, new(2000, 1, 4), 20, 10, "Ticker1", InvestmentType.Stock));

        // Act
        _investmentAccount.Remove(1);

        // Assert
        Assert.NotNull(_investmentAccount.Entries);
        Assert.Single(_investmentAccount.Entries);
        Assert.Equal(10, _investmentAccount.Entries.First().Value);
        Assert.Equal(10, _investmentAccount.Entries.Last().Value);
    }
}