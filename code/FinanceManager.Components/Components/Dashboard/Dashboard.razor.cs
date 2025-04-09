using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Dashboard
{
    public partial class Dashboard : ComponentBase
    {
        private const int UnitHeight = 130;

        [Inject]
        public required IFinancialAccountService FinancalAccountService { get; set; }

        public DateTime StartDateTime { get; set; }
        protected override async Task OnInitializedAsync()
        {
            await GetThisMonth();
        }

        public async Task GetQuater()
        {
            DateTime startDateTime = new(new DateOnly(DateTime.UtcNow.Year, 1, 1), TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc);

            DateTime currentUtcDateTime = DateTime.UtcNow;
            if (currentUtcDateTime.Month <= 3) StartDateTime = startDateTime;
            else if (currentUtcDateTime.Month <= 6) StartDateTime = startDateTime.AddMonths(3);
            else if (currentUtcDateTime.Month <= 9) StartDateTime = startDateTime.AddMonths(6);
            else if (currentUtcDateTime.Month <= 12) StartDateTime = startDateTime.AddMonths(9);

            await Task.CompletedTask;
        }

        public async Task GetThisMonth()
        {
            StartDateTime = new(new DateOnly(DateTime.UtcNow.Year, DateTime.Now.Month, 1), new(), DateTimeKind.Utc);
            await Task.CompletedTask;
        }

        public async Task GetThisYear()
        {
            StartDateTime = new(new DateOnly(DateTime.UtcNow.Year, 1, 1), new(), DateTimeKind.Utc);
            await Task.CompletedTask;
        }
    }
}