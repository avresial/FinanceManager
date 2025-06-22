using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Repositories
{
    public interface IStockRepository
    {
        Task<StockPrice> AddStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date);
        Task<StockPrice?> GetStockPrice(string ticker, DateTime Date);
    }
}
