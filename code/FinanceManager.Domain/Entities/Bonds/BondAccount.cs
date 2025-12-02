using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.Domain.Entities.Bonds;

public class BondAccount : FinancialAccountBase<BondAccountEntry>
{
    public BondAccountEntry? NextOlderEntry { get; set; }
    public BondAccountEntry? NextYoungerEntry { get; set; }
    public AccountLabel AccountType { get; set; }

    public BondAccount(int userId, int accountId, string name, IEnumerable<BondAccountEntry>? entries = null, AccountLabel accountType = AccountLabel.Other,
        BondAccountEntry? nextOlderEntry = null, BondAccountEntry? nextYoungerEntry = null) : base(userId, accountId, name)
    {
        UserId = userId;
        Entries = entries is null ? [] : [.. entries];
        AccountType = accountType;
        NextOlderEntry = nextOlderEntry;
        NextYoungerEntry = nextYoungerEntry;
    }

    public BondAccount(int userId, int id, string name, AccountLabel accountType) : base(userId, id, name)
    {
        AccountType = accountType;
        Entries = [];
    }
    public async Task<Dictionary<DateOnly, decimal>> GetDailyPrice(List<BondDetails> bondDetails)
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
            var entry = GetThisOrNextOlder(index.ToDateTime(TimeOnly.MinValue));
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

    public BondAccountEntry? GetThisOrNextOlder(DateTime date)
    {
        if (Entries is null) return default;
        var result = Entries.GetThisOrNextOlder(date);

        if (result is not null) return result;

        return NextOlderEntry;
    }

}