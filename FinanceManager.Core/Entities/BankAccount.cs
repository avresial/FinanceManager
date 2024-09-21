using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities
{
	public class BankAccount
	{
		public string Name { get; set; }
		public List<BankAccountEntry> Entries { get; set; }
		public AccountType AccountType { get; set; }

		public BankAccount(string name, IEnumerable<BankAccountEntry> entries, AccountType accountType)
		{
			Name = name;
			Entries = entries.ToList();
			AccountType = accountType;
		}
		public BankAccount(string name, AccountType accountType)
		{
			Name = name;
			Entries = new List<BankAccountEntry>();
			AccountType = accountType;
		}

		public List<BankAccountEntry> GetSpendings()
		{
			return null;
		}
		public List<BankAccountEntry> GetSErnings()
		{
			return null;
		}

		public List<BankAccountEntry> GetEntriesMonthly()
		{

			var orderedEntries = Entries.OrderBy(x => x.PostingDate).ToList();
			var beginingDate = orderedEntries.FirstOrDefault().PostingDate.Date;
			var endDate = orderedEntries.LastOrDefault().PostingDate.Date;

			List<BankAccountEntry> result = new List<BankAccountEntry>();

			DateTime stepDate = new DateTime(beginingDate.Year, beginingDate.Month, 1);

			while (stepDate <= endDate)
			{
				var entriesForStepMonth = orderedEntries.Where(x => x.PostingDate.Year == stepDate.Year && x.PostingDate.Month == stepDate.Month);

				if (entriesForStepMonth is null || !entriesForStepMonth.Any())
				{
					stepDate = stepDate.AddMonths(1);
					continue;
				}


				BankAccountEntry bankAccountEntry = new BankAccountEntry()
				{
					PostingDate = stepDate.Date,
					Balance = Math.Round(entriesForStepMonth.Average(x => x.Balance), 2),
					BalanceChange = Math.Round(entriesForStepMonth.Sum(x => x.BalanceChange), 2),
					Description = $"Bank entry of month {stepDate.Month}"
				};

				result.Add(bankAccountEntry);
				stepDate = stepDate.AddMonths(1);
			}


			return result;
		}
	}
}
