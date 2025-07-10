﻿using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class StockPriceRepository(AppDbContext context) : IStockPriceRepository
{
    private readonly AppDbContext _dbContext = context;

    public async Task<StockPrice> Add(string ticker, decimal pricePerUnit, string currency, DateTime date)
    {
        var stockPriceDto = new StockPriceDto(
            0, // Id is autoincremented, will be set by the database
            ticker,
            pricePerUnit,
            currency,
            date.Date
        );

        var entry = await _dbContext.StockPrices.AddAsync(stockPriceDto);
        await _dbContext.SaveChangesAsync();

        return entry.Entity.ToStockPrice();
    }

    public async Task<DateTime?> GetLatestMissing(string ticker)
    {
        DateTime searchDate = DateTime.UtcNow.Date;
        StockPriceDto? stockPrice = await _dbContext.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == searchDate.Date);
        if (stockPrice is null) return searchDate;

        int tries = 365 * 99;
        do
        {
            searchDate = searchDate.AddDays(-1);
            stockPrice = await _dbContext.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == searchDate.Date);
            if (stockPrice is null) return searchDate;

        } while (stockPrice is not null && --tries > 0);

        return null;

    }

    public async Task<StockPrice?> Get(string ticker, DateTime date)
    {
        StockPriceDto? stockPrice = await _dbContext.StockPrices
            .FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == date.Date);

        if (stockPrice is null) return null;


        return stockPrice.ToStockPrice();
    }
    public async Task<StockPrice?> GetThisOrNextOlder(string ticker, DateTime date)
    {
        var result = await Get(ticker, date);
        if (result is not null) return result;

        return await _dbContext.StockPrices
            .Where(x => x.Ticker == ticker && x.Date < date)
            .OrderByDescending(x => x.Date)
            .Select(x => x.ToStockPrice())
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetTickerCurrency(string ticker)
    {
        StockPriceDto? stockPrice = await _dbContext.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker);

        if (stockPrice is null) return null;
        return stockPrice.Currency;
    }

    public async Task<StockPrice?> Update(string ticker, decimal pricePerUnit, string currency, DateTime date)
    {
        StockPriceDto? stockPrice = await _dbContext.StockPrices
                        .FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == date.Date);

        if (stockPrice is null) return null;

        stockPrice.PricePerUnit = pricePerUnit;
        stockPrice.Currency = currency;

        await _dbContext.SaveChangesAsync();

        return stockPrice.ToStockPrice();
    }

    private StockPriceDto GetRandomStockPrice(string ticker, DateTime date, string currency) =>
        new(0, ticker, (decimal)Math.Round(Random.Shared.Next(1, 100) + Random.Shared.NextDouble(), 5), currency, date.Date);

}
