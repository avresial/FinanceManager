using FinanceManager.Domain.Entities;

namespace FinanceManager.Infrastructure.Dtos;

public class StockPriceDto(int id, string ticker, decimal pricePerUnit, string currency, DateTime date, bool verified = false)
{
    public int Id { get; set; } = id;
    public string Ticker { get; set; } = ticker;
    public decimal PricePerUnit { get; set; } = pricePerUnit;
    public string Currency { get; set; } = currency;
    public DateTime Date { get; set; } = date;
    public bool Verified { get; set; } = verified;

    public StockPrice ToStockPrice() => new()
    {
        Ticker = Ticker,
        PricePerUnit = PricePerUnit,
        Currency = Currency,
        Date = Date
    };
};