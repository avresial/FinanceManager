using FinanceManager.Models;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
    public class AllAccountsSummaryBase : ComponentBase
    {
        private Random random = new Random();
        [Parameter]
        public Dictionary<string, List<AccountEntryDto>> Accounts { get; set; }
        public List<Tuple<string, double>> SpendingByCategory { get; set; } = new List<Tuple<string, double>>();
        public List<Tuple<string, double>> WealthByCategory { get; set; } = new List<Tuple<string, double>>();



        protected override async Task OnParametersSetAsync()
        {
            WealthByCategory.Clear();
            WealthByCategory.Add(new Tuple<string, double>("Cash", Math.Round(random.NextDouble(), 2)));
            WealthByCategory.Add(new Tuple<string, double>("Investments", Math.Round(random.NextDouble(), 2)));
            WealthByCategory.Add(new Tuple<string, double>("Assets", Math.Round(random.NextDouble(), 2)));
            
            double otherWealth = 0;
            foreach (var account in Accounts)
            {
                var newValue = account.Value.FirstOrDefault();
                if (newValue is null) continue;

                otherWealth += newValue.Balance;
            }

            WealthByCategory.Add(new Tuple<string, double>("Other", otherWealth));


            SpendingByCategory.Clear();
            SpendingByCategory.Add(new Tuple<string, double>("Investments", Math.Round(random.NextDouble(), 2)));
            SpendingByCategory.Add(new Tuple<string, double>("Car & motocycle", Math.Round(random.NextDouble(), 2)));
            SpendingByCategory.Add(new Tuple<string, double>("Day to day", Math.Round(random.NextDouble(), 2)));
            SpendingByCategory.Add(new Tuple<string, double>("Other", Math.Round(random.NextDouble(), 2)));
        }
    }
}
