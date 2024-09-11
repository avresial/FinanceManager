using FinanceManager.Core.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
	public partial class AllAccountsSummary : ComponentBase
	{
		private Random random = new Random();
		[Parameter]
		public List<BankAccount> Accounts { get; set; }
		public List<Tuple<string, double>> SpendingByCategory { get; set; } = new List<Tuple<string, double>>();
		public List<Tuple<string, double>> WealthByCategory { get; set; } = new List<Tuple<string, double>>();

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





			SpendingByCategory.Clear();
			SpendingByCategory.Add(new Tuple<string, double>("Investments", Math.Round(random.NextDouble(), 2)));
			SpendingByCategory.Add(new Tuple<string, double>("Car & motocycle", Math.Round(random.NextDouble(), 2)));
			SpendingByCategory.Add(new Tuple<string, double>("Day to day", Math.Round(random.NextDouble(), 2)));
			SpendingByCategory.Add(new Tuple<string, double>("Other", Math.Round(random.NextDouble(), 2)));
		}
	}
}
