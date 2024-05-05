using CsvHelper.Configuration;
using CsvHelper;
using FinanceManager.Models;
using FinanceManager.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection.Metadata.Ecma335;

namespace FinanceManager.Pages
{
    public class HomeBase : ComponentBase
    {
        [Inject]
        public AccountsService AccountsService { get; set; }


        public Dictionary<string, List<AccountEntry>> Accounts = new Dictionary<string, List<AccountEntry>>();
        public ObservableCollection<AccountEntry> AccountEntries { get; set; }

        public string AccountName { get; set; }
        public bool isLoading { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                Accounts = AccountsService.Get();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        public async Task GetThisWeek()
        {
            SetAccountsWithinTimeSpan(new TimeSpan(7, 0, 0, 0));
        }

        private void SetAccountsWithinTimeSpan(TimeSpan timeSpan)
        {
            try
            {
                Accounts.Clear();

                foreach (var account in AccountsService.Get())
                {
                    var entries = account.Value.Where(x => (x.PostingDate - DateTime.Now).Duration() < timeSpan).ToList();
                    if (!entries.Any()) continue;

                    Accounts.Add(account.Key, entries);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public async Task GetThisMonth()
        {
            SetAccountsWithinTimeSpan(new TimeSpan(31,0, 0, 0));
        }

        public async Task GetThisYear()
        {
            SetAccountsWithinTimeSpan(new TimeSpan(365, 0, 0, 0));
        }


    }
}

