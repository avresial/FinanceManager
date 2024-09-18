using FinanceManager.Core.Entities;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;

namespace FinanceManager.Infrastructure.Repositories
{
	public class InMemoryMockAccountRepository : IBankAccountRepository
	{
		private readonly Random random = new Random();
		private readonly List<BankAccount> _bankAccounts = new List<BankAccount>();

		public InMemoryMockAccountRepository()
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
			AddAccount(DateTime.UtcNow.AddMonths(-8), 100, "Main", AccountType.Cash);
			AddAccount(DateTime.UtcNow.AddMonths(-8), 10, "Cash", AccountType.Cash);
			AddAccount(DateTime.UtcNow.AddMonths(-8), 10, "Bonds", AccountType.Bond);
			AddAccount(DateTime.UtcNow.AddMonths(-12), 10, "Nvidia", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddMonths(-12), 10, "Intel", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddMonths(-12), 10, "S&P 500", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddMonths(-2), 10, "PPK", AccountType.Stock);
			AddAccount(DateTime.UtcNow.AddYears(-12), 10000, "Apartment", AccountType.RealEstate);

			AddLoanAccount(DateTime.UtcNow.AddMonths(-2), -9000, "Loan");
		}

		private void AddLoanAccount(DateTime startDay, decimal startingBalance, string accountName)
		{
			var creditDays = (DateTime.UtcNow - startDay).TotalDays;
			AddBankAccount(new BankAccount(accountName, AccountType.Loan));
			AddBankAccountEntry(accountName, startingBalance, $"Lorem ipsum {0}", ExpenseType.DebtRepayment, startDay);
			decimal repaidAmount = 0;

			int index = 0;
			while (repaidAmount < -startingBalance && startDay.Date < DateTime.Now.Date)
			{
				decimal balanceChange = (decimal)(random.Next(0, 300) + Math.Round(random.NextDouble(), 2));
				repaidAmount += balanceChange;
				if (repaidAmount >= -startingBalance)
					balanceChange = repaidAmount + startingBalance;
				startDay = startDay.AddDays(1);

				AddBankAccountEntry(accountName, balanceChange, $"Lorem ipsum {index++}", ExpenseType.Other, startDay);

				if (_bankAccounts.Any(x => x.Name == "Main"))
					AddBankAccountEntry("Main", -balanceChange, $"Loan repainment {index++}", ExpenseType.DebtRepayment, startDay);
			}
		}

		private void AddAccount(DateTime startDay, decimal startingBalance, string accountName, AccountType accountType)
		{
			ExpenseType expenseType = GetRandomType();
			if (accountType == AccountType.Stock)
				expenseType = ExpenseType.Investment;

			AddBankAccount(new BankAccount(accountName, accountType));
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
