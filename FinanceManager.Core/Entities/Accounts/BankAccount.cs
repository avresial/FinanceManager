using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialAccountBase
    {
        public string Name { get; set; }
        public virtual DateTime? Start { get; protected set; }
        public virtual DateTime? End { get; protected set; }
    }

    public class FixedAssetAccount : FinancialAccountBase<FixedAssetEntry>
    {
        public FixedAssetAccount(string name) : base(name)
        {

        }
        //	public List<FixedAssetEntry>? Entries { get; private set; }
    }

    public class InvestmentAccount : FinancialAccountBase<InvestmentEntry>//, IFinancalAccount
    {
        public InvestmentAccount(string name, IEnumerable<InvestmentEntry> entries) : base(name)
        {
            Entries = entries.ToList();
        }
        public InvestmentAccount(string name) : base(name)
        {
            Entries = new List<InvestmentEntry>();
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
        public async Task<Dictionary<DateOnly, decimal>> GetDailyPrice(Func<string, DateTime, Task<StockPrice>> getStockPrice)
        {
            var result = new Dictionary<DateOnly, decimal>();
            if (Entries is null || Start is null || End is null) return result;

            DateOnly index = DateOnly.FromDateTime(Start.Value.Date);

            Dictionary<string, decimal> lastTickerValue = new Dictionary<string, decimal>();

            while (index <= DateOnly.FromDateTime(End.Value))
            {
                var entriesOfTheDay = Entries.Where(x => DateOnly.FromDateTime(x.PostingDate) == index);
                decimal dailyPrice = 0;

                var countedTicker = GetStoredTickers();
                foreach (var entry in entriesOfTheDay)
                {
                    countedTicker.RemoveAll(x => x == entry.Ticker);
                    var price = entry.Value * (await getStockPrice(entry.Ticker, index.ToDateTime(new TimeOnly()))).PricePerUnit;
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

        public BankAccount(string name, IEnumerable<BankAccountEntry> entries, AccountType accountType) : base(name)
        {
            Entries = entries.ToList();
            AccountType = accountType;
        }
        public BankAccount(string name, AccountType accountType) : base(name)
        {
            AccountType = accountType;
            Entries = new List<BankAccountEntry>();
        }
        public virtual void GetEntry(DateTime start)
        {
            throw new NotImplementedException();
        }
    }
}
