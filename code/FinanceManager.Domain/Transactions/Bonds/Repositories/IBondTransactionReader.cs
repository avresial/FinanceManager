using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Bonds.Repositories;

public interface IBondTransactionReader
{
    Task<IReadOnlyList<BondLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default);

    /// <summary>Distinct short names of the currencies of the bonds the user has entries in.</summary>
    Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default);
}