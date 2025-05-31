using FinanceManager.Domain.Entities;

namespace FinanceManager.Infrastructure.Dtos;

public record StockPriceDto(int Id, string Ticker, decimal PricePerUnit, string Currency, DateTime Date)
{
    public StockPrice ToStockPrice() => new()
    {
        Ticker = Ticker,
        PricePerUnit = PricePerUnit,
        Currency = Currency,
        Date = Date
    };
};