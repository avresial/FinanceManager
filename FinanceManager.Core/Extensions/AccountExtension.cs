using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Extensions
{
	public static class AccountExtension
	{
		public static List<BankAccountEntry> GetEntriesMonthly(this IList<BankAccountEntry> entries)
		{

			var orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
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


				BankAccountEntry bankAccountEntry = new BankAccountEntry(stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2), Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2))
				{
					Description = $"Bank entry of month {stepDate.Month}"
				};

				result.Add(bankAccountEntry);
				stepDate = stepDate.AddMonths(1);
			}

			return result;
		}
		public static List<BankAccountEntry> GetEntriesWeekly(this IList<BankAccountEntry> entries)
		{

			var orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
			var beginingDate = orderedEntries.FirstOrDefault().PostingDate.Date;
			var endDate = orderedEntries.LastOrDefault().PostingDate.Date;

			List<BankAccountEntry> result = new List<BankAccountEntry>();

			DateTime stepDate = new DateTime(beginingDate.Year, beginingDate.Month, 1);
			stepDate = stepDate.AddDays(-(int)stepDate.DayOfWeek + 1);// might skip one day
			while (stepDate <= endDate)
			{
				var entriesForStepMonth = orderedEntries.Where(x => x.PostingDate >= stepDate && x.PostingDate < stepDate.AddDays(7));

				if (entriesForStepMonth is null || !entriesForStepMonth.Any())
				{
					stepDate = stepDate.AddDays(7);
					continue;
				}


				BankAccountEntry bankAccountEntry = new BankAccountEntry(stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2), Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2))
				{
					Description = $"Bank entry of month {stepDate.Month}"
				};

				result.Add(bankAccountEntry);
				stepDate = stepDate.AddDays(7);
			}

			return result;
		}

		public static List<BankAccountEntry> GetSpendings(this List<BankAccountEntry> entries)
		{
			return null;
		}
		public static List<BankAccountEntry> GetSErnings(this List<BankAccountEntry> entries)
		{
			return null;
		}
	}
}
