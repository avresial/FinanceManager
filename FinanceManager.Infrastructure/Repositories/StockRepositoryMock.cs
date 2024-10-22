using FinanceManager.Core.Entities;
using FinanceManager.Core.Repositories;

namespace FinanceManager.Infrastructure.Repositories
{
    public class StockRepositoryMock : IStockRepository
    {
        private string defaultCurrency = "PLN";
        private readonly Random _random = new Random();
        private readonly Dictionary<string, Dictionary<DateTime, decimal>> _database = new Dictionary<string, Dictionary<DateTime, decimal>>();


        public async Task<StockPrice> GetStockPrice(string ticker, DateTime date)
        {
            if (!_database.ContainsKey(ticker))
                _database.Add(ticker, new Dictionary<DateTime, decimal>());

            var tickerDatabase = _database[ticker];

            if (!tickerDatabase.ContainsKey(date.Date))
                tickerDatabase.Add(date.Date, (decimal)Math.Round(_random.Next(1, 100) + _random.NextDouble(), 5));

            return new StockPrice() { Ticker = ticker, PricePerUnit = tickerDatabase[date.Date], Currency = defaultCurrency, Date = date.Date };
        }
    }
}
