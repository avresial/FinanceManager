using FinanceManager.Core.Entities;
using FinanceManager.Core.Enums;
using FinanceManager.Presentation.ViewModels;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
	public partial class AllAccountsSummary : ComponentBase
	{
		private Random random = new Random();
		[Parameter]
		public List<BankAccount> Accounts { get; set; }
		public List<ExpenseTypeSummaryViewModel> SpendingByCategory { get; set; } = new List<ExpenseTypeSummaryViewModel>();
		public List<Tuple<string, decimal>> WealthByCategory { get; set; } = new List<Tuple<string, decimal>>();

		protected override async Task OnParametersSetAsync()
		{
			WealthByCategory.Clear();

			Dictionary<string, decimal> WealthByCategoryTmp = new Dictionary<string, decimal>();

			foreach (var account in Accounts)
			{
				var newValue = account.Entries.LastOrDefault();
				if (newValue is null) continue;
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
				WealthByCategory.Add(new Tuple<string, decimal>(category.Key, category.Value));

			WealthByCategory = WealthByCategory.OrderBy(x => x.Item1).ToList();

			InitializeSpendingByCathegory();
		}


		void InitializeSpendingByCathegory()
		{
			SpendingByCategory.Clear();
			List<ExpenseType> expenseTypes = Enum.GetValues(typeof(ExpenseType)).Cast<ExpenseType>().ToList();

			foreach (var expenseType in expenseTypes)
				SpendingByCategory.Add(new ExpenseTypeSummaryViewModel() { ExpenseType = expenseType, Value = 0 });

			foreach (var account in Accounts)
			{
				foreach (var expenseType in expenseTypes)
				{
					var cathegory = SpendingByCategory.FirstOrDefault(x => x.ExpenseType == expenseType);
					if (cathegory is null) continue;

					cathegory.Value += account.Entries.Where(x => x.ExpenseType == expenseType).Sum(x => x.BalanceChange);

				}
			}

			SpendingByCategory.RemoveAll(x => x.Value == 0);
		}
	}
}
