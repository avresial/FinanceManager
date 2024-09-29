﻿using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
	public class AccountService(IFinancalAccountRepository bankAccountRepository) : IAccountService
	{
		private readonly IFinancalAccountRepository? _bankAccountRepository = bankAccountRepository;

		public event Action? AccountsChanged;
		public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccount
		{
			if (_bankAccountRepository is null) throw new Exception();
			return _bankAccountRepository.GetAccounts<T>(dateStart, dateEnd);
		}
		public T? GetAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccount
		{
			if (_bankAccountRepository is null) throw new Exception();
			return _bankAccountRepository.GetAccount<T>(name, dateStart, dateEnd);
		}
		public List<T>? GetEntries<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialEntryBase
		{
			return null;
		}
		public void AddFinancialAccount<T>(T bankAccount) where T : FinancialAccount
		{
			_bankAccountRepository?.AddFinancialAccount(bankAccount);
			AccountsChanged?.Invoke();
		}

		public void AddFinancialAccount<AccountType, EntryType>(string name, List<EntryType> data) where AccountType : FinancialAccount where EntryType : FinancialEntryBase
		{
			_bankAccountRepository?.AddFinancialAccount<AccountType, EntryType>(name, data);
		}

		public bool Exists(string name)
		{
			if (_bankAccountRepository is null) throw new Exception();
			return _bankAccountRepository.Exists(name);
		}

		public Dictionary<string, Type> GetAvailableAccounts()
		{
			return _bankAccountRepository.GetAvailableAccounts();
		}
	}
}
