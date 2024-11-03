using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Extensions
{
    public static class AccountEntryExtension
    {
        public static List<FinancialEntryBase> GetEntriesMonthlyValue(this IList<FinancialEntryBase> entries)
        {

            var orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
            var beginingDate = orderedEntries.FirstOrDefault().PostingDate.Date;
            var endDate = orderedEntries.LastOrDefault().PostingDate.Date;

            List<FinancialEntryBase> result = new();

            DateTime stepDate = new DateTime(beginingDate.Year, beginingDate.Month, 1);

            while (stepDate <= endDate)
            {
                var entriesForStepMonth = orderedEntries.Where(x => x.PostingDate.Year == stepDate.Year && x.PostingDate.Month == stepDate.Month);

                if (entriesForStepMonth is null || !entriesForStepMonth.Any())
                {
                    stepDate = stepDate.AddMonths(1);
                    continue;
                }

                FinancialEntryBase bankAccountEntry = new FinancialEntryBase(stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2), Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2));

                result.Add(bankAccountEntry);
                stepDate = stepDate.AddMonths(1);
            }

            return result;
        }
        public static List<FinancialEntryBase> GetEntriesWeekly(this IList<FinancialEntryBase> entries)
        {
            List<FinancialEntryBase> result = new();

            var orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
            if (!orderedEntries.Any()) return result;
            var beginingDate = orderedEntries.FirstOrDefault().PostingDate.Date;
            var endDate = orderedEntries.LastOrDefault().PostingDate.Date;

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
                FinancialEntryBase bankAccountEntry = new FinancialEntryBase(stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2), Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2));

                result.Add(bankAccountEntry);
                stepDate = stepDate.AddDays(7);
            }

            return result;
        }

        public static List<BankAccountEntry> GetSpendings(this List<BankAccountEntry> entries)
        {
            return null;
        }

    }
}
