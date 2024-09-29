namespace FinanceManager.Core.Entities
{
	public class StockPrice
	{
		public required string Ticker { get; set; }
		public decimal Price { get; set; }
		public DateTime Date { get; set; }
	}
}
