using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface ILabelsValueService
{
    Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end);
}