using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Repositories
{
    public interface IStockRepository
    {
        Task<StockPrice> GetStockPrice(string ticker, DateTime Date);
    }
}
