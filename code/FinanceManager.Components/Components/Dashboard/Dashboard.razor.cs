using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Dashboard
{
    public partial class Dashboard : ComponentBase
    {
        private const int _unitHeight = 130;
        public DateTime StartDateTime { get; set; }
        public DateTime EndDate { get; set; } = DateTime.UtcNow;

        [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

        public void DateChanged((DateTime Start, DateTime End) changed)
        {
            StartDateTime = changed.Start;
            EndDate = changed.End;
            StateHasChanged();
        }

    }
}