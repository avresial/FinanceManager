using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
    public partial class Assets
    {
        private const int UnitHeight = 190;

        [Inject]
        public required IFinancalAccountRepository BankAccountRepository { get; set; }

        [Inject]
        public required ISettingsService SettingsService { get; set; }

    }
}