using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

public interface IDividendPaymentRepository
{
    Task<bool> HasPayments(int userId, CancellationToken ct = default);
    Task<DividendPayment?> Get(long id, CancellationToken ct = default);
    // A null accountId reads the owner's payments across every account and the unlinked ones.
    Task<DividendPaymentPage> GetHistory(int userId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<DividendHoldingOption>> GetHoldings(int userId, CancellationToken ct = default);
    Task<bool> HasHolding(int userId, long listingId, CancellationToken ct = default);
    Task<DividendPayment> Add(DividendPayment payment, CancellationToken ct = default);
    Task Save(CancellationToken ct = default);
    Task Delete(DividendPayment payment, CancellationToken ct = default);
}