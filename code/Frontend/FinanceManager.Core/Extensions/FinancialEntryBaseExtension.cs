using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Extensions
{
    public static class FinancialEntryBaseExtension
    {
        public static IEnumerable<T> Get<T>(this IEnumerable<T> entries, DateTime date) where T : FinancialEntryBase
        {
            if (entries is null) return [];
            var result = entries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day);
            if (result.Any()) return result;

            return entries.GetPrevious(date);
        }

        public static IEnumerable<T> GetPrevious<T>(this IEnumerable<T> entries, DateTime date) where T : FinancialEntryBase
        {
            var lastEntry = entries.Where(x => x.PostingDate <= date).FirstOrDefault();
            if (lastEntry is null) return [];

            return [lastEntry];
        }



        public static List<FinancialEntryBase> GetEntriesMonthlyValue(this IEnumerable<FinancialEntryBase> entries)
        {

            List<FinancialEntryBase> orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
            if (!orderedEntries.Any()) return new();

            var beginingDate = orderedEntries.First().PostingDate.Date;
            var endDate = orderedEntries.Last().PostingDate.Date;

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

                int id = entriesForStepMonth.Last().Id;

                FinancialEntryBase bankAccountEntry = new FinancialEntryBase(id, stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2), Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2));

                result.Add(bankAccountEntry);
                stepDate = stepDate.AddMonths(1);
            }

            return result;
        }
        public static List<FinancialEntryBase> GetEntriesWeekly(this IEnumerable<FinancialEntryBase> entries)
        {
            List<FinancialEntryBase> result = new();

            var orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
            if (!orderedEntries.Any()) return result;

            var beginingDate = orderedEntries.First().PostingDate.Date;
            var endDate = orderedEntries.Last().PostingDate.Date;

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
                int id = entriesForStepMonth.Last().Id;
                FinancialEntryBase bankAccountEntry = new FinancialEntryBase(id, stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2), Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2));

                result.Add(bankAccountEntry);
                stepDate = stepDate.AddDays(7);
            }

            return result;
        }
    }
}
