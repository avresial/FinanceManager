using FinanceManager.Core.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
    public partial class AccountDetailsPreview : ComponentBase
    {
        [Parameter]
        public required BankAccount BankAccountModel { get; set; }
    }
}
