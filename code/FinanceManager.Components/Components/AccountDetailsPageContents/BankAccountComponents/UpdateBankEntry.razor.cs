using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents;
public partial class UpdateBankEntry
{

    private int? _loadedEntryId = null;
    private Currency _currency = DefaultCurrency.PLN;
    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time { get; set; } = new TimeSpan(01, 00, 00);

    private string? _description = string.Empty;
    private decimal? _balanceChange = 0;

    private string _labelValue = "Nothing selected";
    private IEnumerable<string> _selectedLabels = [];
    private List<FinancialLabel> _possibleLabels = [];

    [Parameter] public Func<Task>? ActionCompleted { get; set; }
    [Parameter] public required BankAccount BankAccount { get; set; }
    [Parameter] public required BankAccountEntry BankAccountEntry { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var allLabelsCount = await FinancialLabelHttpClient.GetCount();

        _possibleLabels = (await FinancialLabelHttpClient.Get(0, allLabelsCount)).ToList();
    }

    protected override void OnParametersSet()
    {
        if (_loadedEntryId.HasValue && _loadedEntryId.Value == BankAccountEntry.EntryId) return;
        _loadedEntryId = BankAccountEntry.EntryId;

        _currency = settingsService.GetCurrency();
        _postingDate = BankAccountEntry.PostingDate;
        _time = new TimeSpan(BankAccountEntry.PostingDate.Hour, BankAccountEntry.PostingDate.Minute, BankAccountEntry.PostingDate.Second);
        _description = BankAccountEntry.Description;
        _balanceChange = BankAccountEntry.ValueChange;

        _selectedLabels = BankAccountEntry.Labels?.Select(x => x.Name.ToString()).ToList() ?? [];
    }

    public async Task Update()
    {
        if (_form is null) return;
        await _form.Validate();

        if (!_form.IsValid) return;
        if (!_balanceChange.HasValue) return;
        if (!_postingDate.HasValue) return;
        if (!_time.HasValue) return;

        DateTime date = new(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes, _time.Value.Seconds);
        BankAccountEntry bankAccountEntry = new(BankAccountEntry.AccountId, BankAccountEntry.EntryId, date, -1, _balanceChange.Value)
        {
            Description = this._description is null ? string.Empty : this._description,
            Labels = GetLabels().ToList()
        };

        try
        {
            await FinancialAccountService.UpdateEntry(bankAccountEntry);
        }
        catch (Exception ex)
        {
            _errors = [ex.ToString()];
        }

        if (_errors.Length == 0)
        {
            await AccountDataSynchronizationService.AccountChanged();

            if (ActionCompleted is not null)
                await ActionCompleted();
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
        if (ActionCompleted is not null)
            await ActionCompleted();
    }

}