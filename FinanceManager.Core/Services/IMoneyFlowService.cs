using FinanceManager.Core.Entities.MoneyFlowModels;

namespace FinanceManager.Core.Services
{
    public interface IMoneyFlowService
    {
        Task<List<AssetsPerAcountEntry>> GetEndAssetsPerAcount(DateTime start, DateTime end);
    }
}
