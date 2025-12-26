using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Repositories;

public interface IFinancialLabelsRepository
{
    Task<int> GetCount(CancellationToken cancellationToken = default);
    IAsyncEnumerable<FinancialLabel> GetLabels(CancellationToken cancellationToken = default);
    IAsyncEnumerable<FinancialLabel> GetLabelsByAccountId(int userId, CancellationToken cancellationToken = default);
    Task<FinancialLabel> GetLabelsById(int id, CancellationToken cancellationToken = default);
    Task<bool> Add(string name, CancellationToken cancellationToken = default);
    Task<bool> Delete(int id, CancellationToken cancellationToken = default);
    Task<bool> UpdateName(int id, string name, CancellationToken cancellationToken = default);
}