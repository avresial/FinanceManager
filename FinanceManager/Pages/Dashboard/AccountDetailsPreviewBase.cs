using FinanceManager.Models;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
    public class AccountDetailsPreviewBase : ComponentBase
    {
        [Parameter]
        public AccountModel AccountModel { get; set; }
    }
}
