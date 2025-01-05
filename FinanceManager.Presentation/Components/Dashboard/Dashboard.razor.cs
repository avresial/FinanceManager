using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Presentation.Components.Dashboard
{
    public partial class Dashboard : ComponentBase
    {
        private const int UnitHeight = 130;

        [Inject]
        public required IFinancalAccountRepository AccountsService { get; set; }

        public DateTime StartDateTime { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await GetThisMonth();
        }

        public async Task GetQuater()
        {
            DateTime date = DateTime.Now;
            if (date.Month <= 3) StartDateTime = new DateTime(date.Year, 1, 1);
            else if (date.Month <= 6) StartDateTime = new DateTime(date.Year, 4, 1);
            else if (date.Month <= 9) StartDateTime = new DateTime(date.Year, 7, 1);
            else if (date.Month <= 12) StartDateTime = new DateTime(date.Year, 10, 1);
            await Task.CompletedTask;
        }

        public async Task GetThisMonth()
        {
            StartDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            await Task.CompletedTask;
        }

        public async Task GetThisYear()
        {
            StartDateTime = new DateTime(DateTime.Now.Year, 1, 1);
            await Task.CompletedTask;
        }
    }
}