using FinanceManager.Core.Entities.MoneyFlowModels;

namespace FinanceManager.Core.Services
{
    public interface IMoneyFlowService
    {
        List<AssetsPerAcountEntry> GetAssetsPerAcount(DateTime start, DateTime end);
    }
}
