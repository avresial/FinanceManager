using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Dashboard
{
    public partial class Dashboard : ComponentBase
    {
        private const int _unitHeight = 130;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; } = DateTime.UtcNow;

        [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

        protected override void OnInitialized()
        {
            var (Start, End) = DateRangeHelper.GetCurrentMonthRange();

            StartDate = Start;
            EndDate = End;

            base.OnInitialized();
        }

        public void DateChanged((DateTime Start, DateTime End) changed)
        {
            StartDate = changed.Start;
            EndDate = changed.End;
            StateHasChanged();
        }

    }
}