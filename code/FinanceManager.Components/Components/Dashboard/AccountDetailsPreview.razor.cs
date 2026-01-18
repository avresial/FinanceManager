using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Dashboard;

public partial class AccountDetailsPreview : ComponentBase
{
    [Parameter] public required CurrencyAccount CurrencyAccountModel { get; set; }

    public string GetFirstBalance()
    {

        if (CurrencyAccountModel.Entries is null || !CurrencyAccountModel.Entries.Any())
            return "";

        var firstEntry = CurrencyAccountModel.Entries.FirstOrDefault();
        if (firstEntry is null)
            return "";

        return firstEntry.Value.ToString();
    }
    public string GetLastBalance()
    {

        if (CurrencyAccountModel.Entries is null || !CurrencyAccountModel.Entries.Any())
            return "";

        var lastEntry = CurrencyAccountModel.Entries.LastOrDefault();
        if (lastEntry is null)
            return "";

        return lastEntry.Value.ToString();
    }

    public string GetBalanceChange()
    {

        if (CurrencyAccountModel.Entries is null || !CurrencyAccountModel.Entries.Any())
            return "";

        var lastEntry = CurrencyAccountModel.Entries.LastOrDefault();
        if (lastEntry is null)
            return "";

        var firstEntry = CurrencyAccountModel.Entries.FirstOrDefault();
        if (firstEntry is null)
            return "";
        return Math.Round((lastEntry.Value - firstEntry.Value), 2).ToString();
    }

    public string GetFirstPostingDate()
    {

        if (CurrencyAccountModel.Entries is null || !CurrencyAccountModel.Entries.Any())
            return "";

        var firstEntry = CurrencyAccountModel.Entries.FirstOrDefault();
        if (firstEntry is null)
            return "";

        return firstEntry.PostingDate.ToString("yyyy-MM-dd");
    }

    public string GetLastPostingDate()
    {
        if (CurrencyAccountModel.Entries is null || !CurrencyAccountModel.Entries.Any())
            return "";

        var lastEntry = CurrencyAccountModel.Entries.LastOrDefault();
        if (lastEntry is null)
            return "";

        return lastEntry.PostingDate.ToString("yyyy-MM-dd");
    }

}