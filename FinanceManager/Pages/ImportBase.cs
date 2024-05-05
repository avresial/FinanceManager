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
    public class ImportBase : ComponentBase
    {
        [Inject]
        public AccountsService AccountsService { get; set; }

        public List<AccountEntry> CurrentlyLoadedEntries { get; set; }

        public string CurrentlyLoadedAccountName { get; set; }
        public bool IsLoading { get; set; }
        public bool? ImportSucess { get; set; } 
        public string ErrorMessage { get; set; } = string.Empty;



        public async Task LoadFiles(InputFileChangeEventArgs e)
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            //var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            var config = new CsvConfiguration(new CultureInfo("de-DE"))
            {
                Delimiter = ";",
                HasHeaderRecord = true,
            };
            if (e.File is null)
            {
                IsLoading = false;
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
                        CurrentlyLoadedAccountName = Path.GetFileNameWithoutExtension(file.Name);
                        CurrentlyLoadedEntries = await csv.GetRecordsAsync<AccountEntry>().ToListAsync();
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
            }
            ImportSucess = null;
            IsLoading = false;
            //StateHasChanged();
        }

        public void Add()
        {
            if (!AccountsService.Accounts.ContainsKey(CurrentlyLoadedAccountName))
            {
                AccountsService.Accounts.Add(CurrentlyLoadedAccountName, CurrentlyLoadedEntries.ToList());
            }
            else
            {
                ImportSucess = false;
                return;
            }

            CurrentlyLoadedEntries = null;
            ImportSucess = true;
        }
    }
}
