using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Crud;

public partial class AddBondEntry : ComponentBase
{

    private Currency _currency = DefaultCurrency.PLN;
    private bool _success;
    private bool _isAdding;
    private string[] _errors = [];
    private MudForm? _form;

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time = new TimeSpan(01, 00, 00);

    public decimal? ValueChange { get; set; } = null;

    private BondDetails? _selectedBond;
    private List<BondDetails> _possibleBonds = [];

    private string _description = string.Empty;

    private string _labelValue = "Nothing selected";
    private IReadOnlyCollection<string> _selectedLabels = [];
    private List<FinancialLabel> _possibleLabels = [];
    [Parameter] public RenderFragment? CustomButton { get; set; }
    [Parameter] public EventCallback ActionCompleted { get; set; }
    /// <summary>The account to add to. Null adds to the owner's pool of the selected bond (Transactions page).</summary>
    [Parameter] public BondAccount? BondAccount { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required BondTransactionsHttpClient BondTransactionsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<AddBondEntry> Logger { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }


    protected override async Task OnInitializedAsync()
    {
        var allLabelsCount = await FinancialLabelHttpClient.GetCount();
        _possibleLabels = (await FinancialLabelHttpClient.Get(0, allLabelsCount)).ToList();

        _possibleBonds = await BondDetailsHttpClient.GetAll();
    }
    protected override void OnParametersSet()
    {
        _currency = SettingsService.GetCurrency();
    }

    public async Task Add()
    {
        if (_isAdding) return;
        _isAdding = true;
        _errors = [];
        try
        {
            if (_form is null)
            {
                _errors = ["Form initialization error. Please try again."];
                return;
            }

            await _form.Validate();

            if (!_form.IsValid)
            {
                _errors = ["Please correct the validation errors before submitting."];
                return;
            }
            if (!ValueChange.HasValue)
            {
                _errors = ["Value change is required."];
                return;
            }
            if (!_postingDate.HasValue || !_time.HasValue)
            {
                _errors = ["Date and time are required."];
                return;
            }
            if (_selectedBond is null)
            {
                _errors = ["Bond selection is required."];
                return;
            }

            DateTime date = new(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes,
                _time.Value.Seconds);

            var description = _description?.Trim() ?? string.Empty;
            try
            {
                if (BondAccount is null)
                {
                    await BondTransactionsHttpClient.CreateAsync(new SaveBondEntryRequest(
                        _selectedBond.Id, date, ValueChange.Value, description, [.. GetLabels().Select(x => x.Id)]));
                }
                else
                {
                    BondAccountEntry bondAccountEntry = new(BondAccount.AccountId, -1, date, -1, ValueChange.Value, _selectedBond.Id)
                    {
                        Description = description,
                        Labels = GetLabels().ToList()
                    };
                    await FinancialAccountService.AddEntry(bondAccountEntry);
                }
            }
            // Only the owner routes explain a rejection (plan quota, validation); the account services do not.
            catch (HttpRequestException ex) when (BondAccount is null && !string.IsNullOrWhiteSpace(ex.Message))
            {
                Logger.LogWarning(ex, "Rejected entry add");
                _errors = [ex.Message];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding entry");
                _errors = ["Could not add the entry. Please try again."];
            }
            if (_errors.Length == 0)
            {
                _ = AccountDataSynchronizationService.AccountChanged();
                await ActionCompleted.InvokeAsync();
            }
        }
        finally
        {
            _isAdding = false;
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