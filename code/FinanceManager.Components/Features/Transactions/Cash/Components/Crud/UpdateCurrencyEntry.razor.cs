using FinanceManager.Components.Features.Currencies.HttpClients;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Services;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Transactions.Cash.Components.Crud;

public partial class UpdateCurrencyEntry
{

    private long? _loadedEntryId = null;
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
    [Parameter] public required CashEntry CashEntry { get; set; }

    [Inject] public required ILogger<UpdateCurrencyEntry> Logger { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required CashTransactionsHttpClient CashTransactionsHttpClient { get; set; }
    [Inject] public required TransactionDataSynchronizationService TransactionDataSynchronizationService { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var allLabelsCount = await FinancialLabelHttpClient.GetCount();

        _possibleLabels = (await FinancialLabelHttpClient.Get(0, allLabelsCount)).ToList();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedEntryId.HasValue && _loadedEntryId.Value == CashEntry.EntryId) return;
        _loadedEntryId = CashEntry.EntryId;

        // Fill the form before awaiting the currency lookup so the editor never shows (or submits) defaults.
        // Entries are stored as UTC and the rows show them in local time, so the form edits the same local wall-clock time.
        var posted = CashEntry.PostingDate.ToLocalTime();
        _postingDate = posted;
        _time = new TimeSpan(posted.Hour, posted.Minute, posted.Second);
        _description = CashEntry.Description;
        _contractorDetails = CashEntry.ContractorDetails;
        _balanceChange = CashEntry.ValueChange;

        _selectedLabels = CashEntry.Labels?.Select(x => x.Name.ToString()).ToList() ?? [];

        _isResolvingCurrency = true;
        try
        {
            var currencies = await CurrencyHttpClient.GetAll();
            _currency = currencies.FirstOrDefault(x => x.Id == CashEntry.CurrencyId) ?? SettingsService.GetCurrency();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not resolve the currency of the entry; falling back to the user currency.");
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

            // Clear a previous failure so a successful retry closes the editor.
            _errors = [];
            try
            {
                var description = _description ?? string.Empty;
                await CashTransactionsHttpClient.UpdateAsync(CashEntry.EntryId, new UpdateCashEntryRequest(
                    date, _balanceChange.Value, description, _contractorDetails, [.. GetLabels().Select(x => x.Id)]));
            }
            // The owner routes explain a rejection (plan quota, validation); show their message.
            catch (HttpRequestException ex) when (!string.IsNullOrWhiteSpace(ex.Message))
            {
                Logger.LogWarning(ex, "Rejected entry update");
                _errors = [ex.Message];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating entry");
                _errors = ["Could not update the entry. Please try again."];
            }

            if (_errors.Length == 0)
            {
                await TransactionDataSynchronizationService.AccountChanged();

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