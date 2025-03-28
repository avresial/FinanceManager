using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents
{
    public partial class BankAccountDetailsRow
    {
        private bool _expanded = false;
        private bool RemoveEntryVisibility;
        private bool UpdateEntryVisibility;
        internal string currency = "PLN";

        [Parameter]
        public required BankAccount BankAccount { get; set; }

        [Parameter]
        public required BankAccountEntry BankAccountEntry { get; set; }

        [Inject]
        public required IFinancialAccountService FinancalAccountService { get; set; }

        [Inject]
        public required ISettingsService SettingsService { get; set; }

        [Inject]
        public required ILogger<BankAccountDetailsRow> Logger { get; set; }

        protected override void OnParametersSet()
        {
            currency = SettingsService.GetCurrency();
        }

        public async Task Confirm()
        {
            UpdateEntryVisibility = false;
            RemoveEntryVisibility = false;
            _expanded = false;

            try
            {
                await FinancalAccountService.RemoveEntry(BankAccountEntry.EntryId, BankAccount.AccountId);
                BankAccount.Remove(BankAccountEntry.EntryId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while removing entry");
            }

            await InvokeAsync(StateHasChanged);
        }

        public async Task Cancel()
        {
            UpdateEntryVisibility = false;
            RemoveEntryVisibility = false;
            await InvokeAsync(StateHasChanged);
        }

        public async Task HideOverlay()
        {
            UpdateEntryVisibility = false;
            RemoveEntryVisibility = false;
            await InvokeAsync(StateHasChanged);
        }

        public async Task ShowEditOverlay()
        {
            UpdateEntryVisibility = true;
            await Task.CompletedTask;
        }
        public async Task ShowRemoveOverlay()
        {
            RemoveEntryVisibility = true;
            await Task.CompletedTask;
        }

    }
}