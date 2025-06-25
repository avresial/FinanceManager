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

    public async Task<IEnumerable<StockPrice>> GetStockPrice(string ticker, DateTime start, DateTime end, TimeSpan step)
    {
        List<StockPrice> prices = new List<StockPrice>();
        DateTime date = start;
        do
        {
            if (Random.Shared.Next(0, 10) % 9 == 0)
            {
                date = date.Add(step);
                continue;
            }

            prices.Add(new StockPrice()
            {
                Currency = "pln",
                Ticker = ticker,
                Date = date,
                PricePerUnit = Random.Shared.Next(1, 100)
            });
            date = date.Add(step);
        } while (date < end);

        return prices;
    }
}
