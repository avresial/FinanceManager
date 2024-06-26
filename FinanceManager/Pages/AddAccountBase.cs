﻿using FinanceManager.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public class AddAccountBase : ComponentBase
    {
        [Inject]
        public AccountsService AccountsService { get; set; }
        public string AccountName { get; set; }

        public void Add()
        {
            if (!AccountsService.Contains(AccountName))
            {
                AccountsService.Add(AccountName, new List<Models.AccountEntry>());
            }
            else
            {
               // ImportSucess = false;
                return;
            }
            AccountName = "";
           // CurrentlyLoadedEntries = null;
           //  ImportSucess = true;
        }
    }
}
