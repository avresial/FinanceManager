using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Infrastructure.Dtos;

public class StockPriceDto
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal PricePerUnit { get; set; }
    public Currency? Currency { get; set; }
    public DateTime Date { get; set; }
    public bool Verified { get; set; }

    public StockPriceDto()
    {

    }

    public StockPriceDto(int id, string ticker, decimal pricePerUnit, Currency currency, DateTime date, bool verified = false)
    {
        Id = id;
        Ticker = ticker;
        PricePerUnit = pricePerUnit;
        Currency = currency;
        Date = date;
        Verified = verified;
    }

    public StockPrice ToStockPrice() => new()
    {
        Ticker = Ticker,
        PricePerUnit = PricePerUnit,
        Currency = Currency!,
        Date = Date
    };
};