using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public partial class Assets
    {
        const int UnitHeight = 190;

        [Inject]
        public IFinancalAccountRepository BankAccountRepository { get; set; }

        [Inject]
        public ISettingsService SettingsService { get; set; }

    }
}