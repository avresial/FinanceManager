using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

public interface IDividendPaymentRepository
{
    Task<DividendPayment?> Get(long id, CancellationToken ct = default);
    Task<DividendPaymentPage> GetHistory(int userId, int accountId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<DividendHoldingOption>> GetHoldings(int userId, int accountId, CancellationToken ct = default);
    Task<bool> HasHolding(int userId, int accountId, long listingId, CancellationToken ct = default);
    Task<DividendPayment> Add(DividendPayment payment, CancellationToken ct = default);
    Task Save(CancellationToken ct = default);
    Task Delete(DividendPayment payment, CancellationToken ct = default);
}