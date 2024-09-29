using FinanceManager.Core.Repositories;

namespace FinanceManager.Core.Services
{
	public interface IAccountService : IFinancalAccountRepository
	{
		public event Action AccountsChanged;
	}
}
