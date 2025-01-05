using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;
using FinanceManager.Presentation.ViewModels;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
    public partial class AllAccountsSummary : ComponentBase
    {
        [Parameter]
        public List<BankAccount> Accounts { get; set; } = [];

        public List<ExpenseTypeSummaryViewModel> SpendingByCategory { get; set; } = [];
        public List<Tuple<string, decimal>> WealthByCategory { get; set; } = [];

        protected override void OnParametersSet()
        {
            InitializeWealthByCategory();
            InitializeSpendingByCathegory();
        }

        public Dictionary<AccountType, List<BankAccountEntry>> ExpensesTypesAgregate { get; set; } = [];
        public Dictionary<ExpenseType, List<BankAccountEntry>> ExpensesCathegoriesAgregate { get; set; } = [];

        void InitializeWealthByCategory()
        {
            ExpensesTypesAgregate.Clear();
            WealthByCategory.Clear();

            Dictionary<string, decimal> WealthByCategoryTmp = new Dictionary<string, decimal>();

            foreach (var account in Accounts.OrderBy(x => x.Name))
            {
                if (account.Entries is null) continue;

                var newValue = account.Entries.LastOrDefault();
                if (newValue is null) continue;
                if (!WealthByCategoryTmp.ContainsKey(account.AccountType.ToString()))
                {
                    if (newValue is null) continue;
                    WealthByCategoryTmp.Add(account.AccountType.ToString(), newValue.Value);
                    ExpensesTypesAgregate.Add(account.AccountType, account.Entries);
                }
                else
                {
                    WealthByCategoryTmp[account.AccountType.ToString()] += newValue.Value;

                    ExpensesTypesAgregate[account.AccountType].Add(newValue);
                    ExpensesTypesAgregate[account.AccountType] = ExpensesTypesAgregate[account.AccountType].OrderByDescending(x => x.PostingDate).ToList();
                }
            }

            foreach (var category in WealthByCategoryTmp)
                WealthByCategory.Add(new Tuple<string, decimal>(category.Key, category.Value));

            WealthByCategory = WealthByCategory.OrderBy(x => x.Item1).ToList();

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

            DateTime iterationDate = Accounts.Where(x => x is not null && x.Entries is not null)
                                             .Min(x => x.Entries!.Min(z => z.PostingDate));

            while ((iterationDate - DateTime.UtcNow).TotalDays < 0)
            {
                foreach (var account in Accounts)
                {
                    if (account.Entries is null) continue;

                    foreach (var expenseType in expenseTypes)
                    {
                        var cathegory = SpendingByCategory.FirstOrDefault(x => x.ExpenseType == expenseType);
                        if (cathegory is null) continue;

                        var spendingDuringDay = account.Entries.Where(x => x.ExpenseType == expenseType && x.PostingDate.Year == iterationDate.Year &&
                                                                    x.PostingDate.Month == iterationDate.Month && x.PostingDate.Day == iterationDate.Day).ToList();

                        cathegory.Value += spendingDuringDay.Sum(x => x.ValueChange);
                        ExpensesCathegoriesAgregate[expenseType].AddRange(spendingDuringDay);
                    }
                }

                iterationDate = iterationDate.AddDays(1);
            }

        }
    }
}
