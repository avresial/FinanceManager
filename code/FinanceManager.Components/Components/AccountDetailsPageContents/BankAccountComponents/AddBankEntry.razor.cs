using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents;
public partial class AddBankEntry : ComponentBase
{

    private string _currency = string.Empty;
    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time = new TimeSpan(01, 00, 00);

    public string Description { get; set; } = string.Empty;
    public decimal? BalanceChange { get; set; } = null;

    private string _labelValue = "Nothing selected";
    private IEnumerable<string> _selectedLabels = [];
    private List<FinancialLabel> _possibleLabels = [];
    [Parameter] public RenderFragment? CustomButton { get; set; }
    [Parameter] public Func<Task>? ActionCompleted { get; set; }
    [Parameter] public required BankAccount BankAccount { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<AddBankEntry> Logger { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required FinancialLabelHttpContext FinancialLabelHttpContext { get; set; }


    protected override async Task OnInitializedAsync()
    {
        var allLabelsCount = await FinancialLabelHttpContext.GetCount();

        _possibleLabels = (await FinancialLabelHttpContext.Get(0, allLabelsCount)).ToList();
    }
    protected override void OnParametersSet()
    {
        _currency = SettingsService.GetCurrency();
    }

    public async Task Add()
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
        if (!BalanceChange.HasValue)
        {
            _errors = ["Balance change is required."];
            return;
        }
        if (!_postingDate.HasValue || !_time.HasValue)
        {
            _errors = ["Date and time are required."];
            return;
        }

        DateTime date = new(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes,
            _time.Value.Seconds);

        BankAccountEntry bankAccountEntry = new(BankAccount.AccountId, -1, date, -1, BalanceChange.Value)
        {
            Description = Description,
            Labels = GetLabels().ToList()
        };
        try
        {
            await FinancialAccountService.AddEntry(bankAccountEntry);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding entry");
            _errors = [ex.Message];
        }
        if (_errors.Length == 0)
        {
            _ = AccountDataSynchronizationService.AccountChanged();
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