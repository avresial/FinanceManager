using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Infrastructure.Dtos;

public class StockPriceDto
{
    public int Id { get; set; }
    public decimal PricePerUnit { get; set; }
    public StockDetails StockDetails { get; set; } = default!;
    public DateTime Date { get; set; }

    public StockPriceDto()
    {

    }

    public StockPriceDto(int id, StockDetails stockDetails, decimal pricePerUnit, DateTime date)
    {
        Id = id;
        PricePerUnit = pricePerUnit;
        StockDetails = stockDetails;
        Date = date;
    }

    public StockPrice ToStockPrice() => new()
    {
        Ticker = StockDetails.Ticker,
        PricePerUnit = PricePerUnit,
        Currency = StockDetails.Currency,
        Date = Date
    };
};