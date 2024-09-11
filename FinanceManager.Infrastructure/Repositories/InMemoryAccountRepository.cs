using FinanceManager.Core.Entities;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;

namespace FinanceManager.Infrastructure.Repositories
{
	public class InMemoryAccountRepository : IBankAccountRepository
	{
		private readonly Random random = new Random();
		private readonly List<BankAccount> _bankAccounts = new List<BankAccount>();

		public InMemoryAccountRepository()
		{
			AddMockData();
		}

		public void AddBankAccount(BankAccount bankAccount)
		{
			_bankAccounts.Add(bankAccount);
		}

		public void AddBankAccountEntry(string name, List<BankAccountEntry> data)
		{
			var bankAccount = _bankAccounts.FirstOrDefault(x => x.Name == name);

			if (bankAccount is null) return;
			foreach (var item in data)
				AddBankAccountEntry(name, item.BalanceChange, item.SenderName, item.ExpenseType, item.PostingDate);
		}
		public void AddBankAccountEntry(string name, decimal balanceChange, string senderName, ExpenseType expenseType, DateTime? postingDate = null)
		{
			var bankAccount = _bankAccounts.FirstOrDefault(x => x.Name == name);

			if (bankAccount is null) return;

			decimal balance = balanceChange;

			if (bankAccount.Entries.Any())
				balance += bankAccount.Entries.Last().Balance;

			BankAccountEntry bankAccountEntry = new BankAccountEntry()
			{
				BalanceChange = balanceChange,
				Balance = balance,
				SenderName = senderName,
				ExpenseType = expenseType,
				PostingDate = DateTime.Now,
			};

			if (postingDate.HasValue)
				bankAccountEntry.PostingDate = postingDate.Value;

			bankAccount.Entries.Add(bankAccountEntry);
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

		private void AddMockData()
		{
			int elementsCount = 10;

			DateTime dateTime = DateTime.Now - new TimeSpan(elementsCount, 0, 0, 0);
			AddBankAccount(new BankAccount("Main", AccountType.Cash));
			for (int i = 0; i < 10; i++)
				AddBankAccountEntry("Main", GetRandomBalanceChange(), $"Some random sender{i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));

			dateTime = DateTime.Now - new TimeSpan(elementsCount + 7, 0, 0, 0);
			AddBankAccount(new BankAccount("Week ago - Invest", AccountType.Investment));
			for (int i = 0; i < elementsCount; i++)
				AddBankAccountEntry("Week ago - Invest", GetRandomBalanceChange(), $"Some random sender{i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));


			dateTime = DateTime.Now - new TimeSpan(elementsCount + 31, 0, 0, 0);
			AddBankAccount(new BankAccount("Month ago - Asset", AccountType.Asset));
			for (int i = 0; i < 9; i++)
				AddBankAccountEntry("Month ago - Asset", GetRandomBalanceChange(), $"Some random sender{i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));

			dateTime = DateTime.Now.AddMonths(-12);
			AddBankAccount(new BankAccount("Year ago - Asset", AccountType.Other));
			for (int i = 0; i < 9; i++)
				AddBankAccountEntry("Year ago - Asset", GetRandomBalanceChange(), $"Some random sender{i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));
		}



		private ExpenseType GetRandomType()
		{
			Array values = Enum.GetValues(typeof(ExpenseType));
			return (ExpenseType)values.GetValue(random.Next(values.Length));
		}

		private decimal GetRandomBalanceChange()
		{
			return (decimal)(random.Next(-100, 100) + Math.Round(random.NextDouble(), 2));
		}

	}
}
