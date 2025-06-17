using FinanceManager.Domain.Entities;

namespace FinanceManager.Components.HttpContexts;
public class StockPriceHttpContext
{
    public async Task<StockPrice> AddStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date)
    {
        return new StockPrice()
        {
            Currency = "pln",
            Ticker = ticker,
            Date = date,
            PricePerUnit = 1
        };
    }
    public async Task<StockPrice> GetStockPrice(string ticker, DateTime date)
    {
        return new StockPrice()
        {
            Currency = "pln",
            Ticker = ticker,
            Date = date,
            PricePerUnit = 1
        };
    }
}
