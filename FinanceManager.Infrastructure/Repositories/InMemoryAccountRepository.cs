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
				AddBankAccountEntry(name, item.BalanceChange, item.Description, item.ExpenseType, item.PostingDate);
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
				Description = senderName,
				ExpenseType = expenseType,
				PostingDate = DateTime.UtcNow,
			};

			if (postingDate.HasValue)
				bankAccountEntry.PostingDate = postingDate.Value;

			bankAccount.Entries.Add(bankAccountEntry);
		}
		public bool Exists(string name)
		{
			return _bankAccounts.Any(x => x.Name == name);
		}

		public IEnumerable<BankAccount> Get(DateTime dateStart, DateTime dateEnd)
		{
			List<BankAccount> result = new List<BankAccount>();
			foreach (var account in _bankAccounts)
			{
				var newAccount = Get(account.Name, dateStart, dateEnd);
				if (newAccount is null) continue;

				result.Add(newAccount);
			}
			return result;
		}

		public BankAccount? Get(string name, DateTime dateStart, DateTime dateEnd)
		{
			var foundElement = _bankAccounts.FirstOrDefault(x => x.Name == name);

			if (foundElement is null) return null;

			BankAccount result = new BankAccount(foundElement.Name, foundElement.Entries.Where(x => x.PostingDate >= dateStart && x.PostingDate <= dateEnd), foundElement.AccountType);

			return result;
		}

		private void AddMockData()
		{
			int elementsCount = 10;

			DateTime dateTime = DateTime.UtcNow - new TimeSpan(elementsCount, 0, 0, 0);
			AddBankAccount(new BankAccount("Main", AccountType.Cash));
			for (int i = 0; i < elementsCount; i++)
				AddBankAccountEntry("Main", GetRandomBalanceChange(), $"Lorem ipsum{i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));

			dateTime = DateTime.UtcNow - new TimeSpan(elementsCount + 7, 0, 0, 0);
			AddBankAccount(new BankAccount("Bonds", AccountType.Investment));
			for (int i = 0; i < elementsCount; i++)
				AddBankAccountEntry("Bonds", GetRandomBalanceChange(), $"Lorem ipsum{i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));


			dateTime = DateTime.UtcNow - new TimeSpan(elementsCount + 31, 0, 0, 0);
			AddBankAccount(new BankAccount("S&P 500", AccountType.Investment));
			for (int i = 0; i < elementsCount; i++)
				AddBankAccountEntry("S&P 500", GetRandomBalanceChange(), $"Lorem ipsum{i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));

			dateTime = DateTime.UtcNow.AddMonths(-1);
			AddBankAccount(new BankAccount("PPK", AccountType.Investment));
			for (int i = 0; i < elementsCount; i++)
				AddBankAccountEntry("PPK", GetRandomBalanceChange(), $"Lorem ipsum {i}", GetRandomType(), dateTime += new TimeSpan(1, 0, 0, 0));

			dateTime = DateTime.UtcNow.AddMonths(-2);
			var creditDays = (DateTime.UtcNow - dateTime).TotalDays;
			AddBankAccount(new BankAccount("Credit", AccountType.Credit));
			AddBankAccountEntry("Credit", -9000, $"Lorem ipsum {0}", ExpenseType.DeptRepainment, dateTime += new TimeSpan(1, 0, 0, 0));
			for (int i = 1; i < creditDays; i++)
				AddBankAccountEntry("Credit", (decimal)(random.Next(0, 300) + Math.Round(random.NextDouble(), 2)), $"Lorem ipsum {i}", ExpenseType.DeptRepainment, dateTime += new TimeSpan(1, 0, 0, 0));
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
