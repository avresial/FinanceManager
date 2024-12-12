using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Globalization;

namespace FinanceManager.Presentation.Components.ImportData
{
    public partial class ImportBankEntriesComponent : ComponentBase
    {
        public List<string> RequiredHeders = new List<string>() { "Id", "PostingDate", "ValueChange" };

        [Parameter]
        public required string AccountName { get; set; }

        [Inject]
        public IFinancalAccountRepository BankAccountRepository { get; set; }

        public List<BankAccountEntry> CurrentlyLoadedEntries { get; set; }

        private string currentlyLoadedAccountName;
        public string CurrentlyLoadedAccountName
        {
            get { return currentlyLoadedAccountName; }
            set
            {
                currentlyLoadedAccountName = value;
                IsDisableUpdate();
            }
        }

        public bool IsLoading { get; set; }
        public bool? ImportSucess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsDisabled { get; set; }
        public void IsDisableUpdate()
        {
            IsDisabled = BankAccountRepository.AccountExists(CurrentlyLoadedAccountName);
        }

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



            foreach (var file in e.GetMultipleFiles(6))
            {
                try
                {
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    using (var csv = new CsvReader(reader, config))
                    {
                        await csv.ReadAsync();
                        csv.ReadHeader();
                        var headers = csv.HeaderRecord;
                        if (headers is null) continue;
                        bool containsAllColums = true;

                        foreach (var item in RequiredHeders)
                        {
                            if (!headers.Contains(item))
                                containsAllColums = false;
                        }

                        if (!containsAllColums)
                        {
                            ErrorMessage = "Lacks some fields";
                            continue;
                        }
                    }

                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
            }

            CloseImportNotification();
            IsLoading = false;
        }

        public void CloseImportNotification()
        {
            ImportSucess = null;
        }
        public void Add()
        {
            if (!BankAccountRepository.AccountExists(CurrentlyLoadedAccountName))
            {
                BankAccountRepository.AddFinancialAccount<BankAccount, BankAccountEntry>(CurrentlyLoadedAccountName, CurrentlyLoadedEntries.ToList());
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