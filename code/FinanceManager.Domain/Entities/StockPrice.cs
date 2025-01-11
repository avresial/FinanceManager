namespace FinanceManager.Domain.Entities
{
    public class StockPrice
    {
        public required string Ticker { get; set; }
        public decimal PricePerUnit { get; set; }
        public required string Currency { get; set; }
        public DateTime Date { get; set; }
    }
}
