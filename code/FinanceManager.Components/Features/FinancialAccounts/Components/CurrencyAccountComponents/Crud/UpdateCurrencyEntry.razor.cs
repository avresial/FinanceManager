using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Crud;

public partial class UpdateCurrencyEntry
{

    private int? _loadedEntryId = null;
    private Currency _currency = DefaultCurrency.PLN;
    private bool _isResolvingCurrency = true;
    private bool _isUpdating;
    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time = new TimeSpan(01, 00, 00);

    private string? _description = string.Empty;
    private string? _contractorDetails;
    private decimal? _balanceChange = 0;

    private string _labelValue = "Nothing selected";
    private IReadOnlyCollection<string> _selectedLabels = [];
    private List<FinancialLabel> _possibleLabels = [];

    [Parameter] public EventCallback ActionCompleted { get; set; }
    [Parameter] public required CurrencyAccount CurrencyAccount { get; set; }
    [Parameter] public required CurrencyAccountEntry CurrencyAccountEntry { get; set; }

    [Inject] public required ILogger<UpdateCurrencyEntry> Logger { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var allLabelsCount = await FinancialLabelHttpClient.GetCount();

        _possibleLabels = (await FinancialLabelHttpClient.Get(0, allLabelsCount)).ToList();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedEntryId.HasValue && _loadedEntryId.Value == CurrencyAccountEntry.EntryId) return;
        _loadedEntryId = CurrencyAccountEntry.EntryId;

        // Fill the form before awaiting the currency lookup so the editor never shows (or submits) defaults.
        _postingDate = CurrencyAccountEntry.PostingDate;
        _time = new TimeSpan(CurrencyAccountEntry.PostingDate.Hour, CurrencyAccountEntry.PostingDate.Minute, CurrencyAccountEntry.PostingDate.Second);
        _description = CurrencyAccountEntry.Description;
        _contractorDetails = CurrencyAccountEntry.ContractorDetails;
        _balanceChange = CurrencyAccountEntry.ValueChange;

        _selectedLabels = CurrencyAccountEntry.Labels?.Select(x => x.Name.ToString()).ToList() ?? [];

        _isResolvingCurrency = true;
        try
        {
            var currencies = await CurrencyHttpClient.GetAll();
            _currency = currencies.FirstOrDefault(x => x.Id == CurrencyAccount.CurrencyId) ?? SettingsService.GetCurrency();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not resolve the currency of account {AccountId}; falling back to the user currency.", CurrencyAccount.AccountId);
            _currency = SettingsService.GetCurrency();
        }
        finally
        {
            _isResolvingCurrency = false;
        }
    }

    public async Task Update()
    {
        if (_form is null || _isResolvingCurrency || _isUpdating) return;
        _isUpdating = true;
        try
        {
            await _form.Validate();

            if (!_form.IsValid) return;
            if (!_balanceChange.HasValue) return;
            if (!_postingDate.HasValue) return;
            if (!_time.HasValue) return;

            DateTime date = new(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes, _time.Value.Seconds);
            CurrencyAccountEntry accountEntry = new(CurrencyAccountEntry.AccountId, CurrencyAccountEntry.EntryId, date, -1, _balanceChange.Value)
            {
                Description = this._description is null ? string.Empty : this._description,
                ContractorDetails = this._contractorDetails,
                Labels = GetLabels().ToList()
            };

            // Clear a previous failure so a successful retry closes the editor.
            _errors = [];
            try
            {
                await FinancialAccountService.UpdateEntry(accountEntry);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating entry");
                _errors = ["Could not update the entry. Please try again."];
            }

            if (_errors.Length == 0)
            {
                await AccountDataSynchronizationService.AccountChanged();

                await ActionCompleted.InvokeAsync();
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public IEnumerable<FinancialLabel> GetLabels()
    {
        if (_selectedLabels is null || _selectedLabels.Count() == 0) yield break;

        foreach (var selectedLabel in _selectedLabels)
        {
            var existingLabel = _possibleLabels.FirstOrDefault(x => x.Name == selectedLabel);
            if (existingLabel is null) continue;
            yield return existingLabel;
        }
    }
    public async Task Cancel()
    {
        await ActionCompleted.InvokeAsync();
    }

}