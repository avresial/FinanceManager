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
                Accounts = AccountsService.Accounts;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }



        public async Task LoadFiles(InputFileChangeEventArgs e)
        {
            isLoading = true;
            ErrorMessage = string.Empty;
            //var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            var config = new CsvConfiguration(new CultureInfo("de-DE"))
            {
                Delimiter = ";",
                HasHeaderRecord = true,
            };
            if (e.File is null)
            {
                isLoading = false;
                return;
            }

            foreach (var file in e.GetMultipleFiles(1))
            {
                try
                {
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    using (var csv = new CsvReader(reader, config))
                    {
                        await Task.Delay(1000);
                        AccountName = Path.GetFileNameWithoutExtension(file.Name);
                        AccountEntries = new ObservableCollection<AccountEntry>(await csv.GetRecordsAsync<AccountEntry>().ToListAsync());
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
            }

            isLoading = false;
            StateHasChanged();
        }

        public async Task Add()
        {
            if (!AccountsService.Accounts.ContainsKey(AccountName))
                AccountsService.Accounts.Add(AccountName, AccountEntries.ToList());

            AccountEntries = null;
        }
    }
}
