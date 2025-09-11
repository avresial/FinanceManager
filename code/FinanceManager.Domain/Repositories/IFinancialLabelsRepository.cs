using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Domain.Repositories;
public interface IFinancialLabelsRepository
{
    Task<int> GetCount();
    IAsyncEnumerable<FinancialLabel> GetLabels();
    IAsyncEnumerable<FinancialLabel> GetLabelsByAccountId(int userId);
    Task<FinancialLabel> GetLabelsById(int id);
    Task<bool> Add(string name);
    Task<bool> Delete(int id);
    Task<bool> UpdateName(int id, string name);
}
