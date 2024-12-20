﻿using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;

namespace FinanceManager.Pages
{
    public partial class Home : ComponentBase
    {
        [Inject]
        public IFinancalAccountRepository AccountsService { get; set; }

        [Inject]
        public ILoginService LoginService { get; set; }
        [Inject]
        public NavigationManager Navigation { get; set; }

        public List<BankAccount> Accounts;
        public ObservableCollection<BankAccountEntry> AccountEntries { get; set; }

        public string AccountName { get; set; }
        public bool isLoading { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            var loggedUser = await LoginService.GetLoggedUser();

            if (loggedUser is null)
            {
                Navigation.NavigateTo("login");
            }
            //await GetThisMonth();
        }

        private void SetAccountsWithinTimeSpan(TimeSpan timeSpan)
        {
            try
            {
                if (Accounts is null) Accounts = new List<BankAccount>();
                Accounts.Clear();

                foreach (BankAccount account in AccountsService.GetAccounts<BankAccount>(DateTime.Now.AddDays(-31), DateTime.Now))
                {
                    List<BankAccountEntry> entries = account.Entries.Where(x => (DateTime.Now - x.PostingDate).Duration() < timeSpan).ToList();
                    if (!entries.Any()) continue;

                    Accounts.Add(new BankAccount(account.Id, account.Name, entries, account.AccountType));
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        public async Task GetAllTime()
        {
            try
            {
                Accounts = AccountsService.GetAccounts<BankAccount>(DateTime.Now.AddDays(-31), DateTime.Now).ToList();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        public async Task GetThisMonth()
        {
            //	var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var date = DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day);
            SetAccountsWithinTimeSpan((DateTime.UtcNow - date).Duration());
        }

        public async Task GetThisYear()
        {
            var date = new DateTime(DateTime.UtcNow.Year, 1, 1);
            SetAccountsWithinTimeSpan((DateTime.UtcNow - date).Duration());
        }


    }
}

