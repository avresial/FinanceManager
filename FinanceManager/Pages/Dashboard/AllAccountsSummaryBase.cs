using FinanceManager.Models;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
    public class AllAccountsSummaryBase : ComponentBase
    {
        private Random random = new Random();
        [Parameter]
        public Dictionary<string, List<AccountEntry>> Accounts { get; set; }

        public double Spent { get; set; }
        public double Invested { get; set; }
        public double FundsLeft { get; set; }
        public double AllFunds { get; set; }
        public double SpentOnCar { get; set; }
        public double DayToDay { get; set; }
        public double Investments { get; set; }
        public double Rest { get; set; }
        
        protected override async Task OnParametersSetAsync()
        {
            Spent = Math.Round(random.NextDouble(), 2);
            Invested = Math.Round(random.NextDouble(), 2);
            FundsLeft = Math.Round(random.NextDouble(), 2);
            AllFunds = Math.Round(random.NextDouble(), 2);
            SpentOnCar = Math.Round(random.NextDouble(), 2);
            DayToDay = Math.Round(random.NextDouble(), 2);
            Investments = Math.Round(random.NextDouble(), 2);
            Rest = Math.Round(random.NextDouble(), 2);
        }
    }
}
