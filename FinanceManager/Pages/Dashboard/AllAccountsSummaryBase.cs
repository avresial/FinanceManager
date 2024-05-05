using ChartJs.Blazor.Common;
using ChartJs.Blazor.LineChart;
using ChartJs.Blazor.PieChart;
using ChartJs.Blazor.Util;
using FinanceManager.Models;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
    public class AllAccountsSummaryBase : ComponentBase
    {
        private Random random = new Random();
        [Parameter]
        public List<AccountModel> Accounts { get; set; }
        public List<Tuple<string, double>> SpendingByCategory { get; set; } = new List<Tuple<string, double>>();
        public List<Tuple<string, double>> WealthByCategory { get; set; } = new List<Tuple<string, double>>();
        public LineConfig _config;
        public LineConfig _config2;

        protected override async Task OnInitializedAsync()
        {
            _config = new LineConfig
            {
                Options = new LineOptions
                {
                    Responsive = true,
                    AspectRatio = 3,
                    Title = new OptionsTitle
                    {
                        Display = true,
                        Text = "Total wealth chart"
                    }
                }
            };

            foreach (string color in new[] { "Red", "Yellow", "Green", "Blue" })
            {
                _config.Data.Labels.Add(color);
            }

            LineDataset<int> dataset = new LineDataset<int>(new[] { 6, 5, 3, 7 });


            _config.Data.Datasets.Add(dataset);


            _config2 = new LineConfig
            {
                Options = new LineOptions
                {
                    Responsive = true,
                    AspectRatio = 3,

                    Title = new OptionsTitle
                    {
                        Display = true,
                        Text = "Spending chart"
                    },
                }
            };
            _config2.Data.Datasets.Add(dataset2);
        }
        LineDataset<int> dataset2 = new LineDataset<int>();
        protected override async Task OnParametersSetAsync()
        {
            WealthByCategory.Clear();

            Dictionary<string, double> WealthByCategoryTmp = new Dictionary<string, double>();

            foreach (var account in Accounts)
            {
                var newValue = account.Entries.LastOrDefault();
                if (!WealthByCategoryTmp.ContainsKey(account.AccountType.ToString()))
                {
                    if (newValue is null) continue;
                    WealthByCategoryTmp.Add(account.AccountType.ToString(), newValue.Balance);
                }
                else
                {
                    WealthByCategoryTmp[account.AccountType.ToString()] += newValue.Balance;
                }
            }

            foreach (var category in WealthByCategoryTmp)
                WealthByCategory.Add(new Tuple<string, double>(category.Key, category.Value));
            WealthByCategory = WealthByCategory.OrderBy(x => x.Item1).ToList();


            
            for (int i = 0; i < random.Next(1, 5); i++)
                dataset2.Add(i);
           // _config2.Data.Datasets.Clear();
           

            SpendingByCategory.Clear();
            SpendingByCategory.Add(new Tuple<string, double>("Investments", Math.Round(random.NextDouble(), 2)));
            SpendingByCategory.Add(new Tuple<string, double>("Car & motocycle", Math.Round(random.NextDouble(), 2)));
            SpendingByCategory.Add(new Tuple<string, double>("Day to day", Math.Round(random.NextDouble(), 2)));
            SpendingByCategory.Add(new Tuple<string, double>("Other", Math.Round(random.NextDouble(), 2)));
        }
    }
}
