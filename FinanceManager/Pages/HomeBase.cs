﻿using ChartJs.Blazor.Common;
using ChartJs.Blazor.PieChart;
using ChartJs.Blazor.Util;
using FinanceManager.Models;
using FinanceManager.Services;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;

namespace FinanceManager.Pages
{
    public class HomeBase : ComponentBase
    {
        [Inject]
        public AccountsService AccountsService { get; set; }


        public List<AccountModel> Accounts = new List<AccountModel>();
        public ObservableCollection<AccountEntryDto> AccountEntries { get; set; }

        public string AccountName { get; set; }
        public bool isLoading { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;


        protected override async Task OnInitializedAsync()
        {
            GetAllTime();
        }


        private void SetAccountsWithinTimeSpan(TimeSpan timeSpan)
        {
            try
            {
                Accounts.Clear();

                foreach (var account in AccountsService.Get())
                {
                    List<AccountEntryDto> entries = account.Entries.Where(x => (x.PostingDate - DateTime.Now).Duration() < timeSpan).ToList();
                    if (!entries.Any()) continue;

                    Accounts.Add(account);
                }
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
                Accounts = AccountsService.Get();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        public async Task GetThisMonth()
        {
            var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            SetAccountsWithinTimeSpan((DateTime.Now - date).Duration());
        }

        public async Task GetThisYear()
        {
            var date = new DateTime(DateTime.Now.Year, 1, 1);
            SetAccountsWithinTimeSpan((DateTime.Now - date).Duration());
        }


    }
}

