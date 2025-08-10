using FinanceManager.Domain.Entities.Accounts.Entries;

namespace FinanceManager.Domain.Repositories;
public interface IFinancialLabelsRepository
{
    Task<List<FinancialLabel>> GetLabels();
}
