using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;

namespace FinanceManager.Core.Entities.Accounts
{
    public interface IFinancalAccount
    {
        void SetDates(DateTime start, DateTime end);

        void SetDaily();
        void SetMonthly();
        void SetYearly();

        void SetExpenses();
        void SetEarnings();

    }

    public class FinancialAccountBase
    {
        public string Name { get; set; }
        public DateTime Start { get; protected set; }
        public DateTime End { get; protected set; }
    }

    public class FinancialAccountBase<T> : FinancialAccountBase, IFinancalAccount where T : FinancialEntryBase
    {
        public List<T>? Entries { get; protected set; }

        public FinancialAccountBase()
        {

        }
        public FinancialAccountBase(string name, DateTime start, DateTime end)
        {
            Name = name;
            Start = start;
            End = end;
        }

        public virtual void SetDates(DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }

        public void SetDaily()
        {
            throw new NotImplementedException();
        }

        public void SetMonthly()
        {
            throw new NotImplementedException();
        }

        public void SetYearly()
        {
            throw new NotImplementedException();
        }

        public void SetExpenses()
        {
            throw new NotImplementedException();
        }

        public void SetEarnings()
        {
            throw new NotImplementedException();
        }
    }

    public class FixedAssetAccount : FinancialAccountBase<FixedAssetEntry>
    {
        //	public List<FixedAssetEntry>? Entries { get; private set; }
        public override void SetDates(DateTime start, DateTime end)
        {

            base.SetDates(start, end);
        }
    }

    public class StockAccount : FinancialAccountBase<InvestmentEntry>//, IFinancalAccount
    {
        public StockAccount(string name, IEnumerable<InvestmentEntry> entries)
        {
            Name = name;
            Entries = entries.ToList();
        }
        public StockAccount(string name, DateTime start, DateTime end) : base(name, start, end)
        {
            Entries = new List<InvestmentEntry>();
        }
        public override void SetDates(DateTime start, DateTime end)
        {
            if (Entries is null) return;
            Entries.RemoveAll(x => x.PostingDate < start || x.PostingDate > end);

            if (!Entries.Any()) return;

            Start = Entries.Last().PostingDate;
            End = Entries.First().PostingDate;
        }

        public List<InvestmentType> GetStoredTypes()
        {
            if (Entries is null) Enumerable.Empty<InvestmentType>();

            return Entries.DistinctBy(x => x.InvestmentType).Select(x => x.InvestmentType).ToList();
        }
        public List<string> GetStoredTickers()
        {
            if (Entries is null) return new List<string>();

            return Entries.DistinctBy(x => x.Ticker).Select(x => x.Ticker).ToList();
        }
        public async Task<Dictionary<DateOnly, decimal>> GetDailyPrice(IStockRepository stockRepository)
        {
            var result = new Dictionary<DateOnly, decimal>();
            if (Entries is null) return result;

            DateOnly index = DateOnly.FromDateTime(Start.Date);

            Dictionary<string, decimal> lastTickerValue = new Dictionary<string, decimal>();

            while (index <= DateOnly.FromDateTime(End))
            {
                var entriesOfTheDay = Entries.Where(x => DateOnly.FromDateTime(x.PostingDate) == index);
                decimal dailyPrice = 0;

                var countedTicker = GetStoredTickers();
                foreach (var entry in entriesOfTheDay)
                {
                    countedTicker.RemoveAll(x => x == entry.Ticker);
                    var price = entry.Value * (await stockRepository.GetStockPrice(entry.Ticker, index.ToDateTime(new TimeOnly()))).PricePerUnit;
                    if (!lastTickerValue.ContainsKey(entry.Ticker))
                        lastTickerValue.Add(entry.Ticker, price);
                    else
                        lastTickerValue[entry.Ticker] = price;
                    dailyPrice += price;
                }

                foreach (var item in countedTicker)
                {
                    if (!lastTickerValue.ContainsKey(item)) continue;
                    dailyPrice += lastTickerValue[item];
                }

                result.Add(index, dailyPrice);
                index = index.AddDays(+1);
            }

            return result;
        }
    }

    public class BankAccount : FinancialAccountBase<BankAccountEntry>//, IFinancalAccount
    {
        //public List<BankAccountEntry>? Entries { get; private set; }
        public AccountType AccountType { get; private set; }

        public BankAccount(string name, IEnumerable<BankAccountEntry> entries, AccountType accountType)
        {
            Name = name;
            Entries = entries.ToList();
            AccountType = accountType;
        }
        public BankAccount(string name, AccountType accountType)
        {
            Name = name;
            AccountType = accountType;
            Entries = new List<BankAccountEntry>();
        }

        public override void SetDates(DateTime start, DateTime end)
        {
            if (Entries is null) return;

            Entries.RemoveAll(x => x.PostingDate < start || x.PostingDate > end);

            if (!Entries.Any()) return;

            Start = Entries.Last().PostingDate;
            End = Entries.First().PostingDate;
        }
    }
}
