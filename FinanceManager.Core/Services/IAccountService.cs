using FinanceManager.Core.Repositories;

namespace FinanceManager.Core.Services
{
    [Obsolete("This service should be no longer in use. Use AccountDataSynchronizationService.cs")]
    public interface IAccountService : IFinancalAccountRepository
    {
        public event Action AccountsChanged;
    }
}
