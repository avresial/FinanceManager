using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.Domain.Entities.Stocks;

public class StockAccount : FinancialAccountBase<StockAccountEntry>
{
    public readonly Dictionary<string, StockAccountEntry> NextOlderEntries = [];
    public readonly Dictionary<string, StockAccountEntry> NextYoungerEntries = [];

    public StockAccount(int userId, int accountId, string name, IEnumerable<StockAccountEntry> entries, Dictionary<string, StockAccountEntry>? nextOlderEntries = null,
        Dictionary<string, StockAccountEntry>? nextYoungerEntries = null)
        : base(userId, accountId, name)
    {
        Entries = entries.ToList();
        NextOlderEntries = nextOlderEntries ?? [];
        NextYoungerEntries = nextYoungerEntries ?? [];
    }
    public StockAccount(int userId, int id, string name) : base(userId, id, name)
    {
        Entries = [];
    }

    public List<InvestmentType> GetStoredTypes()
    {
        if (Entries is null) return [];

        return Entries.GetStoredTypes();
    }
    public List<string> GetStoredTickers()
    {
        if (Entries is null) return [];

        return Entries.DistinctBy(x => x.Ticker).Select(x => x.Ticker).ToList();
    }
    public async Task<Dictionary<DateOnly, decimal>> GetDailyPrice(Func<string, DateTime, Task<StockPrice?>> getStockPrice)
    {
        var result = new Dictionary<DateOnly, decimal>();
        if (Entries is null || Start is null || End is null) return result;

        DateOnly index = DateOnly.FromDateTime(Start.Value.Date);

        Dictionary<string, decimal> lastTickerValue = [];

        while (index <= DateOnly.FromDateTime(End.Value))
        {
            var entriesOfTheDay = Entries.Where(x => DateOnly.FromDateTime(x.PostingDate) == index);
            decimal dailyPrice = 0;

            var countedTicker = GetStoredTickers();
            foreach (var entry in entriesOfTheDay)
            {
                countedTicker.RemoveAll(x => x == entry.Ticker);

                decimal price = entry.Value;
                var stockPrice = await getStockPrice(entry.Ticker, index.ToDateTime(new TimeOnly(), DateTimeKind.Utc));
                if (stockPrice is not null)
                    price = entry.Value * stockPrice.PricePerUnit;

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
    public override IEnumerable<StockAccountEntry> Get(DateTime date)
    {
        if (Entries is null) return [];
        return Entries.Get(date);
    }
    public StockAccountEntry? GetThisOrNextOlder(DateTime date, string ticker)
    {
        if (Entries is null) return default;
        var result = Entries.GetThisOrNextOlder(date, ticker);

        if (result is not null) return result;
        if (!NextOlderEntries.ContainsKey(ticker)) return default;

        return NextOlderEntries[ticker];
    }
    public int GetNextFreeId()
    {
        var currentMaxId = GetMaxId();
        if (currentMaxId is not null)
            return currentMaxId.Value + 1;
        return 0;
    }
    public void Add(AddInvestmentEntryDto entry, bool recalculateValues = true) =>
        Add(new StockAccountEntry(AccountId, -1, entry.PostingDate, -1, entry.ValueChange, entry.Ticker, entry.InvestmentType), recalculateValues);

    public override void Add(IEnumerable<StockAccountEntry> entries, bool recalculateValues = true)
    {
        foreach (var entry in entries)
            Add(entry, recalculateValues);
    }
    public override void Add(StockAccountEntry entry, bool recalculateValues = true)
    {
        var alreadyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.Ticker == entry.Ticker && x.ValueChange == entry.ValueChange);
        if (alreadyExistingEntry is not null)
        {
            throw new Exception($"WARNING -  Entry already exist, can not be added: Id:{alreadyExistingEntry.EntryId}, Posting date{alreadyExistingEntry.PostingDate}, " +
                $"Ticker {alreadyExistingEntry.Ticker}, Value change {alreadyExistingEntry.ValueChange}");
        }

        var previousEntry = Entries.GetNextYounger(entry.PostingDate).FirstOrDefault();
        var index = previousEntry is null ? -1 : Entries.IndexOf(previousEntry);

        if (index == -1)
        {
            Entries.Add(entry);
            index = Entries.Count - 1;
        }
        else
        {
            Entries.Insert(index, entry);
        }

        if (recalculateValues)
            RecalculateEntryValues(index);

    }
    public override void UpdateEntry(StockAccountEntry entry, bool recalculateValues = true)
    {
        Entries ??= [];

        var entryToUpdate = Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);
        if (entryToUpdate is null) return;

        entryToUpdate.Update(entry);
        Entries.Remove(entryToUpdate);
        var previousEntry = Entries.GetNextYounger(entryToUpdate.PostingDate).FirstOrDefault();

        if (previousEntry is null)
        {
            Entries.Add(entryToUpdate);
        }
        else
        {
            var newIndex = Entries.IndexOf(previousEntry);
            Entries.Insert(newIndex, entryToUpdate);
        }

        var index = Entries.IndexOf(entryToUpdate);
        if (recalculateValues)
            RecalculateEntryValues(index);
    }
    public override void Remove(int id)
    {
        if (Entries is null) return;

        var entry = Entries.FirstOrDefault(x => x.EntryId == id);
        if (entry is null) return;
        var index = Entries.IndexOf(entry);

        Entries.RemoveAt(index);
        RecalculateEntryValues(index - 1);
    }

    private new void RecalculateEntryValues(int? startingIndex)
    {
        if (Entries is null) return;
        int startIndex = startingIndex ?? Entries.Count - 1;

        StockAccountEntry? previousIterationEntry = null;
        for (int i = startIndex; i >= 0; i--)
        {
            if (Entries.Count < i) continue;

            var previousElements = Entries.GetNextOlder(Entries[i].PostingDate, Entries[i].Ticker);
            if (previousElements is not null && previousElements.Any())
                previousIterationEntry = previousElements.FirstOrDefault();

            if (previousIterationEntry is not null)
            {
                Entries[i].Value = previousIterationEntry.Value + Entries[i].ValueChange;
            }
            else
            {
                Entries[i].Value = Entries[i].ValueChange;
            }


            previousIterationEntry = Entries[i];
        }
    }
}