using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.Domain.Entities.Bonds;

public class BondAccount : FinancialAccountBase<BondAccountEntry>
{
    public readonly Dictionary<int, BondAccountEntry> NextOlderEntries = [];
    public readonly Dictionary<int, BondAccountEntry> NextYoungerEntries = [];

    public AccountLabel AccountType { get; set; }

    public BondAccount(int userId, int accountId, string name, IEnumerable<BondAccountEntry>? entries = null, AccountLabel accountType = AccountLabel.Other,
        Dictionary<int, BondAccountEntry>? nextOlderEntries = null, Dictionary<int, BondAccountEntry>? nextYoungerEntries = null) : base(userId, accountId, name)
    {
        UserId = userId;
        Entries = entries is null ? [] : [.. entries];
        AccountType = accountType;
        NextOlderEntries = nextOlderEntries ?? [];
        NextYoungerEntries = nextYoungerEntries ?? [];
    }

    public BondAccount(int userId, int id, string name, AccountLabel accountType) : base(userId, id, name)
    {
        AccountType = accountType;
        Entries = [];
    }
    public Dictionary<DateOnly, decimal> GetDailyPrice(List<BondDetails> bondDetails)
    {
        var result = new Dictionary<DateOnly, decimal>();
        if (Entries is null || Start is null || End is null) return result;

        DateOnly index = DateOnly.FromDateTime(Start.Value.Date);
        var detailsIds = Entries.Select(e => e.BondDetailsId).Distinct().ToList();
        if (!detailsIds.All(id => bondDetails.Any(bd => bd.Id == id)))
            throw new ArgumentException("Not all BondDetails are provided for the entries in this account.");
        List<Dictionary<DateOnly, decimal>> pricesPerDetail = [];
        foreach (var id in detailsIds)
        {
            var entry = GetThisOrNextOlder(index.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), id);
            if (entry is null) return result;

            pricesPerDetail.Add(entry.GetPrice(DateOnly.FromDateTime(End.Value), bondDetails.Single(bd => bd.Id == entry.BondDetailsId)));
        }

        foreach (var price in pricesPerDetail.SelectMany(dict => dict))
        {
            if (result.ContainsKey(price.Key))
                result[price.Key] += price.Value;
            else
                result[price.Key] = price.Value;
        }

        return result;
    }

    public BondAccountEntry? GetThisOrNextOlder(DateTime date, int bondDetailsId)
    {
        if (Entries is null) return default;
        var result = Entries.GetThisOrNextOlder(date, bondDetailsId);

        if (result is not null) return result;
        if (!NextOlderEntries.ContainsKey(bondDetailsId)) return default;

        return NextOlderEntries[bondDetailsId];
    }

    public List<int> GetStoredBondsIds()
    {
        if (Entries is null) return [];

        return Entries.DistinctBy(x => x.BondDetailsId).Select(x => x.BondDetailsId).ToList();
    }

}