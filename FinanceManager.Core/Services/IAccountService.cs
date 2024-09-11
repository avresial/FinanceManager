using FinanceManager.Core.Repositories;

namespace FinanceManager.Core.Services
{
	public interface IAccountService : IBankAccountRepository
	{
		public event Action AccountsChanged;
	}
}
