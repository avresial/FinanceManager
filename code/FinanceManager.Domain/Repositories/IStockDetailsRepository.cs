using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Repositories;

public interface IStockDetailsRepository
{
    Task<StockDetails?> Get(string ticker, CancellationToken ct = default);
    Task<IReadOnlyList<StockDetails>> GetAll(CancellationToken ct = default);
    Task<StockDetails> Add(StockDetails details, CancellationToken ct = default);
    Task<bool> Delete(string ticker, CancellationToken ct = default);
}
