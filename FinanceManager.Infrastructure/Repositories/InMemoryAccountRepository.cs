using FinanceManager.Core.Entities;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;

namespace FinanceManager.Infrastructure.Repositories
{
	public class InMemoryAccountRepository : IBankAccountRepository
	{
		private readonly List<BankAccount> _bankAccounts = new()
		{
			new("Main", new List<BankAccountEntry>() { new() { PostingDate = DateTime.Now, Balance = 10 } }, AccountType.Asset),
			new("Week ago", [new() { PostingDate = DateTime.Now - new TimeSpan(2, 0, 0, 0), Balance = 20 }], AccountType.Investment),
			new("Month ago", new List<BankAccountEntry>() { new() { PostingDate = DateTime.Now - new TimeSpan(20, 0, 0, 0), Balance = 30 } }, AccountType.Other)
		};


		public void AddBankAccount(BankAccount bankAccount)
		{
			_bankAccounts.Add(bankAccount);
		}

		public void AddBankAccountEntry(string name, List<BankAccountEntry> data)
		{
			var bankAccount = _bankAccounts.FirstOrDefault(x => x.Name == name);

			if (bankAccount is null) return;

			bankAccount.Entries.AddRange(data);
		}

		public bool Exists(string name)
		{
			return _bankAccounts.Any(x => x.Name == name);
		}

		public IEnumerable<BankAccount> Get()
		{
			return _bankAccounts;
		}

		public BankAccount? Get(string name)
		{
			return _bankAccounts.FirstOrDefault(x => x.Name == name);
		}
	}
}
