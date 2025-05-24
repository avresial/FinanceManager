using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories
{
    public class StockRepositoryMock(StockPricesContext stockPricesContext) : IStockRepository
    {
        private readonly StockPricesContext _stockPricesContext = stockPricesContext;
        private readonly string _defaultCurrency = "PLN";

        public async Task<StockPrice> GetStockPrice(string ticker, DateTime date)
        {
            StockPriceDto? stockPrice = await _stockPricesContext.StockPrices
                .FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == date.Date);

            if (stockPrice is null)
            {
                stockPrice = GetRandomStockPrice(ticker, date);

                await _stockPricesContext.AddAsync(stockPrice);
                await _stockPricesContext.SaveChangesAsync();
            }

            return stockPrice.ToStockPrice();
        }

        private StockPriceDto GetRandomStockPrice(string ticker, DateTime date) =>
            new(0, ticker, (decimal)Math.Round(Random.Shared.Next(1, 100) + Random.Shared.NextDouble(), 5), _defaultCurrency, date.Date);

        public async Task<StockPrice> AddStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date)
        {
            var stockPriceDto = new StockPriceDto(
                0, // Id will be set by the database
                ticker,
                pricePerUnit,
                currency,
                date.Date
            );

            var entry = await _stockPricesContext.StockPrices.AddAsync(stockPriceDto);
            await _stockPricesContext.SaveChangesAsync();

            return entry.Entity.ToStockPrice();
        }
    }
}
