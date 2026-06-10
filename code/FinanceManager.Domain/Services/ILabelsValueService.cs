using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface ILabelsValueService
{
    Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end);
}