using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;
using System.ComponentModel.Design;

namespace FinanceManager.Infrastructure.Repositories
{
	public class InMemoryMockAccountRepository : IFinancalAccountRepository
	{
		private readonly Random random = new Random();
		private readonly ServiceContainer _bankAccounts = new ServiceContainer();

		public InMemoryMockAccountRepository()
		{
			AddMockData();
		}

		public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccount
		{
			var accountsOfType = _bankAccounts.GetService(typeof(List<T>)) as List<T>;
			if (accountsOfType is null)
				return Enumerable.Empty<T>();

			List<T> result = new List<T>();
			foreach (var account in accountsOfType)
			{
				if (account is null) continue;

				string name = account.Name;
				var newAccount = GetAccount<T>(name, dateStart, dateEnd);
				if (newAccount is null) continue;

				result.Add(newAccount);
			}
			return result;
		}

		public T? GetAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccount
		{
			List<T> accountsOfType = _bankAccounts.GetService(typeof(List<T>)) as List<T>;
			if (accountsOfType is null) return default;

			var firstAccount = accountsOfType.FirstOrDefault(x => x.Name == name);
			if (firstAccount is null) return default;

			if (typeof(T) == typeof(BankAccount))
			{
				BankAccount databaseBankAccount = firstAccount as BankAccount;

				BankAccount bankAccount = new BankAccount(name, databaseBankAccount.Entries, databaseBankAccount.AccountType);
				bankAccount.SetDates(dateStart, dateEnd);
				return bankAccount as T;
			}

			return default;
		}
		public List<T>? GetEntries<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialEntryBase
		{


			return null;
		}

		public void AddFinancialAccount<T>(T bankAccount) where T : FinancialAccount
		{
			var accountsOfType = (List<T>)_bankAccounts.GetService(typeof(List<T>));
			if (accountsOfType is null)
				_bankAccounts.AddService(typeof(List<T>), new List<T>() { bankAccount });
			else
				accountsOfType.Add(bankAccount);
		}


		public void AddFinancialAccount<AccountType, EntryType>(string name, List<EntryType> data) where AccountType : FinancialAccount where EntryType : FinancialEntryBase
		{
			//var bankAccount = _bankAccounts.FirstOrDefault(x => x.Name == name);

			//if (bankAccount is null) return;
			//foreach (var item in data)
			//	AddFinancialAccount(name, item.BalanceChange, item.Description, item.ExpenseType, item.PostingDate);
		}

		public bool Exists(string name)
		{
			return false;
			//			return _bankAccounts.Any(x => x.Name == name);
		}

		private T? FindAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccount
		{
			List<T> accountsOfType = _bankAccounts.GetService(typeof(List<T>)) as List<T>;
			if (accountsOfType is null) return default;

			var firstAccount = accountsOfType.FirstOrDefault(x => x.Name == name);
			if (firstAccount is null) return default;

			return firstAccount;
		}
		private void AddBankAccountEntry(string name, decimal balanceChange, string senderName, ExpenseType expenseType, DateTime? postingDate = null)
		{
			var bankAccount = FindAccount<BankAccount>(name, new DateTime(), DateTime.Now);
			if (bankAccount is null) return;

			decimal balance = balanceChange;

			if (bankAccount.Entries is not null && bankAccount.Entries.Any())
				balance += bankAccount.Entries.Last().Value;
			//DateTime postingDate = DateTime.Now;
			BankAccountEntry bankAccountEntry = new BankAccountEntry(postingDate.HasValue ? postingDate.Value : DateTime.UtcNow, balance, balanceChange)
			{
				Description = senderName,
				ExpenseType = expenseType,
			};
			AddFinancialAccount<BankAccount, BankAccountEntry>(name, new List<BankAccountEntry>() { bankAccountEntry });
			if (bankAccount.Entries is not null)
				bankAccount.Entries.Add(bankAccountEntry);
		}
		private void AddMockData()
		{
			AddAccount(DateTime.UtcNow.AddMonths(-8), 100, "Main", AccountType.Cash);
			AddAccount(DateTime.UtcNow.AddMonths(-8), 10, "Cash", AccountType.Cash);
			AddAccount(DateTime.UtcNow.AddMonths(-8), 10, "Bonds", AccountType.Bond);
			AddAccount(DateTime.UtcNow.AddMonths(-12), 10, "Nvidia", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddMonths(-12), 10, "Intel", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddMonths(-12), 10, "S&P 500", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddMonths(-2), 10, "PPK", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddYears(-12), 10000, "Apartment", AccountType.RealEstate);

			//	AddLoanAccount(DateTime.UtcNow.AddMonths(-2), -300 * 62, "Loan");
		}

		//private void AddLoanAccount(DateTime startDay, decimal startingBalance, string accountName)
		//{
		//	var creditDays = (DateTime.UtcNow - startDay).TotalDays;
		//	AddFinancialAccount(new BankAccount(accountName, AccountType.Loan));
		//	AddFinancialAccount(accountName, startingBalance, $"Lorem ipsum {0}", ExpenseType.DebtRepayment, startDay);
		//	decimal repaidAmount = 0;

		//	int index = 0;
		//	while (repaidAmount < -startingBalance && startDay.Date < DateTime.Now.Date)
		//	{
		//		decimal balanceChange = (decimal)(random.Next(0, 300) + Math.Round(random.NextDouble(), 2));
		//		repaidAmount += balanceChange;
		//		if (repaidAmount >= -startingBalance)
		//			balanceChange = repaidAmount + startingBalance;
		//		startDay = startDay.AddDays(1);

		//		AddFinancialAccount(accountName, balanceChange, $"Lorem ipsum {index++}", ExpenseType.Other, startDay);

		//		if (_bankAccounts.Any(x => x.Name == "Main"))
		//			AddFinancialAccount("Main", -balanceChange, $"Loan repainment {index++}", ExpenseType.DebtRepayment, startDay);
		//	}
		//}

		private void AddAccount(DateTime startDay, decimal startingBalance, string accountName, AccountType accountType)
		{
			ExpenseType expenseType = GetRandomType();
			if (accountType == AccountType.Stock)
				expenseType = ExpenseType.Investment;

			AddFinancialAccount(new BankAccount(accountName, accountType));
			AddBankAccountEntry(accountName, startingBalance, $"Lorem ipsum {0}", expenseType, startDay);

			int index = 0;
			while (startDay.Date < DateTime.Now.Date)
			{
				decimal balanceChange = (decimal)(random.Next(-150, 200) + Math.Round(random.NextDouble(), 2));

				startDay = startDay.AddDays(1);
				expenseType = GetRandomType();
				if (accountType == AccountType.Stock)
					expenseType = ExpenseType.Investment;

				AddBankAccountEntry(accountName, balanceChange, $"Lorem ipsum {index++}", expenseType, startDay);
			}
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
