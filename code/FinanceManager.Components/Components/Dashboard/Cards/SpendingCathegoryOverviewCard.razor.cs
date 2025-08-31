using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class SpendingCathegoryOverviewCard
    {
        private string currency = string.Empty;
        private List<SpendingCathegoryOverviewEntry> Data = new();

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }
        [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

        [Inject] public required ILogger<SpendingCathegoryOverviewCard> Logger { get; set; }
        [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
        [Inject] public required ISettingsService settingsService { get; set; }
        [Inject] public required ILoginService loginService { get; set; }

        protected override void OnInitialized()
        {
            currency = settingsService.GetCurrency();
        }

        protected override async Task OnParametersSetAsync()
        {
            Data.Clear();
            var user = await loginService.GetLoggedUser();
            if (user is null) return;
            IEnumerable<BankAccount> bankAccounts = [];
            try
            {
                bankAccounts = await FinancalAccountService.GetAccounts<BankAccount>(user.UserId, StartDateTime, EndDateTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while getting bank accounts");
            }

            foreach (var account in bankAccounts)
            {
                if (account.Entries is null || !account.Entries.Any()) continue;
                foreach (var entry in account.Entries.Where(x => x.ValueChange < 0))
                {
                    //var key = entry.ExpenseType.ToString();
                    //var entryElement = Data.FirstOrDefault(x => x.ExpenseType == entry.ExpenseType);

                    //if (entryElement is not null)
                    //{
                    //    entryElement.Value += -entry.ValueChange;
                    //}
                    //else
                    //{
                    //    Data.Add(new SpendingCathegoryOverviewEntry() { Name = "Unknown", Value = -entry.ValueChange });
                    //}
                }
            }

            Data = Data.OrderByDescending(x => x.Value).ToList();
        }

        private class SpendingCathegoryOverviewEntry
        {
            public string Name { get; set; }
            public decimal Value { get; set; }
        }

    }
}