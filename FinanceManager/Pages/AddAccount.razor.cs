﻿using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public partial class AddAccount : ComponentBase
    {
        [Inject]
        public IAccountService AccountService { get; set; }
        public string AccountName { get; set; }

        public void Add()
        {
            if (!AccountService.AccountExists(AccountName))
            {
                var lastAccountId = AccountService.GetLastAccountId();

                AccountService.AddFinancialAccount(new BankAccount(++lastAccountId, AccountName, Core.Enums.AccountType.Other));
                StateHasChanged();
            }
            else
            {
                return;
            }
            AccountName = "";
        }
    }
}
