using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Dashboard
{
    public partial class Dashboard : ComponentBase
    {
        private const int _unitHeight = 130;
        public DateTime StartDateTime { get; set; }

        [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await GetThisMonth();
        }
        public async Task GetQuater()
        {
            DateTime currentUtcDateTime = DateTime.UtcNow;
            if (currentUtcDateTime.Month <= 3) StartDateTime = new(new DateOnly(DateTime.UtcNow.Year, 1, 1), TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc);
            else if (currentUtcDateTime.Month <= 6) StartDateTime = new(new DateOnly(DateTime.UtcNow.Year, 4, 1), TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc);
            else if (currentUtcDateTime.Month <= 9) StartDateTime = new(new DateOnly(DateTime.UtcNow.Year, 7, 1), TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc);
            else if (currentUtcDateTime.Month <= 12) StartDateTime = new(new DateOnly(DateTime.UtcNow.Year, 10, 1), TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc);

            await Task.CompletedTask;
        }

        public async Task GetThisMonth()
        {
            StartDateTime = new(new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), new(), DateTimeKind.Utc);
            await Task.CompletedTask;
        }

        public async Task GetThisYear()
        {
            StartDateTime = new(new DateOnly(DateTime.UtcNow.Year, 1, 1), new(), DateTimeKind.Utc);
            await Task.CompletedTask;
        }
    }
}