using FinanceManager.Core.Entities;

namespace FinanceManager.Core.Repositories
{
	public interface IStockRepository
	{
		Task<StockPrice> GetStockPrice(string ticker, DateTime Date);
	}
}
