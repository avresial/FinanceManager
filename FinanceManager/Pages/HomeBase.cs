using CsvHelper.Configuration;
using CsvHelper;
using FinanceManager.Models;
using FinanceManager.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.ObjectModel;
using System.Globalization;

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
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }


    }
}

