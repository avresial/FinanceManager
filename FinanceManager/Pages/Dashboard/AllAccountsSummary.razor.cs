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

			InitializeWealthByCategory();
			InitializeSpendingByCathegory();
		}

		public Dictionary<AccountType, List<BankAccountEntry>> ExpensesTypesAgregate { get; set; } = new Dictionary<AccountType, List<BankAccountEntry>>();
		public Dictionary<ExpenseType, List<BankAccountEntry>> ExpensesCathegoriesAgregate { get; set; } = new Dictionary<ExpenseType, List<BankAccountEntry>>();

		void InitializeWealthByCategory()
		{
			ExpensesTypesAgregate.Clear();
			WealthByCategory.Clear();

			Dictionary<string, decimal> WealthByCategoryTmp = new Dictionary<string, decimal>();

			foreach (var account in Accounts.OrderBy(x => x.Name))
			{
				var newValue = account.Entries.LastOrDefault();
				if (newValue is null) continue;
				if (!WealthByCategoryTmp.ContainsKey(account.AccountType.ToString()))
				{
					if (newValue is null) continue;
					WealthByCategoryTmp.Add(account.AccountType.ToString(), newValue.Balance);
					ExpensesTypesAgregate.Add(account.AccountType, account.Entries);
				}
				else
				{
					WealthByCategoryTmp[account.AccountType.ToString()] += newValue.Balance;

					ExpensesTypesAgregate[account.AccountType].Add(newValue);
					ExpensesTypesAgregate[account.AccountType] = ExpensesTypesAgregate[account.AccountType].OrderByDescending(x => x.PostingDate).ToList();
				}
			}

			foreach (var category in WealthByCategoryTmp)
				WealthByCategory.Add(new Tuple<string, decimal>(category.Key, category.Value));

			WealthByCategory = WealthByCategory.OrderBy(x => x.Item1).ToList();

			foreach (var item in ExpensesTypesAgregate.Values)
			{
				foreach (var element in item)
				{
					element.PostingDate = new DateTime(element.PostingDate.Year, element.PostingDate.Month, element.PostingDate.Day);
				}
			}
		}
		void InitializeSpendingByCathegory()
		{
			SpendingByCategory.Clear();
			ExpensesCathegoriesAgregate.Clear();

			List<ExpenseType> expenseTypes = Enum.GetValues(typeof(ExpenseType)).Cast<ExpenseType>().ToList();

			foreach (var expenseType in expenseTypes)
			{
				ExpensesCathegoriesAgregate.Add(expenseType, new List<BankAccountEntry>());
				SpendingByCategory.Add(new ExpenseTypeSummaryViewModel() { ExpenseType = expenseType, Value = 0 });
			}

			DateTime iterationDate = Accounts.Min(x => x.Entries.Min(z => z.PostingDate));

			while ((iterationDate - DateTime.UtcNow).TotalDays < 0)
			{


				foreach (var account in Accounts)
				{
					foreach (var expenseType in expenseTypes)
					{
						var cathegory = SpendingByCategory.FirstOrDefault(x => x.ExpenseType == expenseType);
						if (cathegory is null)
						{
							ExpensesCathegoriesAgregate[expenseType].Add(new BankAccountEntry() { PostingDate = iterationDate, ExpenseType = expenseType });
							continue;
						}

						var spendingDuringDay = account.Entries.Where(x => x.ExpenseType == expenseType && x.PostingDate.Year == iterationDate.Year && x.PostingDate.Month == iterationDate.Month && x.PostingDate.Day == iterationDate.Day).ToList();
						if (!spendingDuringDay.Any())
						{
							ExpensesCathegoriesAgregate[expenseType].Add(new BankAccountEntry() { PostingDate = iterationDate, ExpenseType = expenseType });
						}

						cathegory.Value += spendingDuringDay.Sum(x => x.BalanceChange);
						ExpensesCathegoriesAgregate[expenseType].AddRange(spendingDuringDay);
					}
				}

				iterationDate = iterationDate.AddDays(1);
			}

		}
	}
}
