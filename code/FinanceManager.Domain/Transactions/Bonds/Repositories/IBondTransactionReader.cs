using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Bonds.Repositories;

public interface IBondTransactionReader
{
    Task<IReadOnlyList<BondLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default);
}