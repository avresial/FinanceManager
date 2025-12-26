using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.BondAccountComponents;

public partial class UpdateBondEntry
{

    private int? _loadedEntryId = null;
    private Currency _currency = DefaultCurrency.PLN;
    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time { get; set; } = new TimeSpan(01, 00, 00);

    private decimal? _valueChange = 0;

    private BondDetails? _selectedBond;
    private List<BondDetails> _possibleBonds = [];

    private string _labelValue = "Nothing selected";
    private IEnumerable<string> _selectedLabels = [];
    private List<FinancialLabel> _possibleLabels = [];

    [Parameter] public Func<Task>? ActionCompleted { get; set; }
    [Parameter] public required BondAccount BondAccount { get; set; }
    [Parameter] public required BondAccountEntry BondAccountEntry { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var allLabelsCount = await FinancialLabelHttpClient.GetCount();
        _possibleLabels = (await FinancialLabelHttpClient.Get(0, allLabelsCount)).ToList();

        _possibleBonds = await BondDetailsHttpClient.GetAll();
        SetSelectedBond();
    }

    protected override void OnParametersSet()
    {
        if (_loadedEntryId.HasValue && _loadedEntryId.Value == BondAccountEntry.EntryId) return;
        _loadedEntryId = BondAccountEntry.EntryId;

        _currency = settingsService.GetCurrency();
        _postingDate = BondAccountEntry.PostingDate;
        _time = new TimeSpan(BondAccountEntry.PostingDate.Hour, BondAccountEntry.PostingDate.Minute, BondAccountEntry.PostingDate.Second);
        _valueChange = BondAccountEntry.ValueChange;

        SetSelectedBond();

        _selectedLabels = BondAccountEntry.Labels?.Select(x => x.Name.ToString()).ToList() ?? [];
    }

    private void SetSelectedBond()
    {
        if (_possibleBonds.Count > 0)
        {
            _selectedBond = _possibleBonds.FirstOrDefault(b => b.Id == BondAccountEntry.BondDetailsId);
        }
    }

    public async Task Update()
    {
        if (_form is null) return;
        await _form.Validate();

        if (!_form.IsValid) return;
        if (!_valueChange.HasValue) return;
        if (!_postingDate.HasValue) return;
        if (!_time.HasValue) return;
        if (_selectedBond is null) return;

        DateTime date = new(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes, _time.Value.Seconds);
        BondAccountEntry bondAccountEntry = new(BondAccountEntry.AccountId, BondAccountEntry.EntryId, date, -1, _valueChange.Value, _selectedBond.Id)
        {
            Labels = GetLabels().ToList()
        };

        try
        {
            await FinancialAccountService.UpdateEntry(bondAccountEntry);
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