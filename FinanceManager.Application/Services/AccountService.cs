using FinanceManager.Core.Entities;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
	public class AccountService(IBankAccountRepository bankAccountRepository) : IAccountService
	{
		private readonly IBankAccountRepository? _bankAccountRepository = bankAccountRepository;

		public event Action? AccountsChanged;

		public void AddBankAccount(BankAccount bankAccount)
		{
			_bankAccountRepository?.AddBankAccount(bankAccount);
			AccountsChanged?.Invoke();
		}

		public void AddBankAccountEntry(string name, List<BankAccountEntry> data)
		{
			_bankAccountRepository?.AddBankAccountEntry(name, data);
		}

		public bool Exists(string name)
		{
			if (_bankAccountRepository is null) throw new Exception();
			return _bankAccountRepository.Exists(name);
		}

		public IEnumerable<BankAccount> Get(DateTime dateStart, DateTime dateEnd)
		{
			if (_bankAccountRepository is null) throw new Exception();
			return _bankAccountRepository.Get(dateStart, dateEnd);
		}

		public BankAccount? Get(string name, DateTime dateStart, DateTime dateEnd)
		{
			if (_bankAccountRepository is null) throw new Exception();
			return _bankAccountRepository.Get(name, dateStart, dateEnd);
		}
	}
}
