using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Services;

public interface IAssetsService
{
    Task<bool> IsAnyAccountWithAssets(int userId);
    Task<List<NameValueResult>> GetEndAssetsPerAccount(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<NameValueResult>> GetEndAssetsPerType(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType);
}
