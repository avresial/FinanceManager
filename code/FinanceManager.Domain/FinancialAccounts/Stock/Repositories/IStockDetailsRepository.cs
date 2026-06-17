using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Repositories;

public interface IStockDetailsRepository
{
    Task<StockDetails?> Get(string isin, CancellationToken ct = default);
    Task<StockDetails?> GetByTicker(string ticker, CancellationToken ct = default);
    Task<IReadOnlyList<StockDetails>> GetAll(CancellationToken ct = default);
    Task<StockDetails> Add(StockDetails details, CancellationToken ct = default);
    Task<bool> Delete(string isin, CancellationToken ct = default);
}