using FinanceManager.Core.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
    public partial class AccountDetailsPreview : ComponentBase
    {
        [Parameter]
        public required BankAccount BankAccountModel { get; set; }

        public string GetFirstBalance()
        {

            if (BankAccountModel.Entries is null || !BankAccountModel.Entries.Any())
                return "";

            var firstEntry = BankAccountModel.Entries.FirstOrDefault();
            if (firstEntry is null)
                return "";

            return firstEntry.Balance.ToString();
        }
        public string GetLastBalance()
        {

            if (BankAccountModel.Entries is null || !BankAccountModel.Entries.Any())
                return "";

            var lastEntry = BankAccountModel.Entries.LastOrDefault();
            if (lastEntry is null)
                return "";

            return lastEntry.Balance.ToString();
        }

        public string GetBalanceChange()
        {

            if (BankAccountModel.Entries is null || !BankAccountModel.Entries.Any())
                return "";

            var lastEntry = BankAccountModel.Entries.LastOrDefault();
            if (lastEntry is null)
                return "";

            var firstEntry = BankAccountModel.Entries.FirstOrDefault();
            if (firstEntry is null)
                return "";
            return Math.Round((lastEntry.Balance - firstEntry.Balance), 2).ToString();
        }

        public string GetFirstPostingDate()
        {

            if (BankAccountModel.Entries is null || !BankAccountModel.Entries.Any())
                return "";

            var firstEntry = BankAccountModel.Entries.FirstOrDefault();
            if (firstEntry is null)
                return "";

            return firstEntry.PostingDate.ToString("yyyy-MM-dd");
        }

        public string GetLastPostingDate()
        {

            if (BankAccountModel.Entries is null || !BankAccountModel.Entries.Any())
                return "";

            var lastEntry = BankAccountModel.Entries.LastOrDefault();
            if (lastEntry is null)
                return "";

            return lastEntry.PostingDate.ToString("yyyy-MM-dd");
        }

    }
}
