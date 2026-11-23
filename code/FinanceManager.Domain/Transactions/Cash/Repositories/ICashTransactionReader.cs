using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Cash.Repositories;

public interface ICashTransactionReader
{
    Task<IReadOnlyList<CashLogEntry>> GetHistory(int userId, OwnerHistoryQuery query, CancellationToken ct = default);

    /// <summary>Distinct short names of the currencies the user has cash entries in.</summary>
    Task<IReadOnlyList<string>> GetCurrencies(int userId, CancellationToken ct = default);
}