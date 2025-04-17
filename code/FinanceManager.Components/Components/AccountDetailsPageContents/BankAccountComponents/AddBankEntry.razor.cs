using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
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

    private readonly List<string> _expenseTypes = Enum.GetValues<ExpenseType>().Cast<ExpenseType>().Select(x => x.ToString()).ToList();

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time = new TimeSpan(01, 00, 00);

    public string ExpenseType { get; set; } = Domain.Enums.ExpenseType.Other.ToString();
    public string Description { get; set; } = string.Empty;
    public decimal? BalanceChange { get; set; } = null;

    [Parameter] public RenderFragment? CustomButton { get; set; }
    [Parameter] public Func<Task>? ActionCompleted { get; set; }
    [Parameter] public required BankAccount BankAccount { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<AddBankEntry> Logger { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

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

        DateTime date = new DateTime(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes, _time.Value.Seconds);
        ExpenseType expenseType = FinanceManager.Domain.Enums.ExpenseType.Other;
        try
        {
            expenseType = (ExpenseType)Enum.Parse(typeof(ExpenseType), ExpenseType);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing expense type");
        }

        var id = 0;
        var currentMaxId = BankAccount.GetMaxId();
        if (currentMaxId is not null)
            id += currentMaxId.Value + 1;

        BankAccountEntry bankAccountEntry = new(BankAccount.AccountId, id, date, -1, BalanceChange.Value)
        {
            Description = Description,
            ExpenseType = expenseType
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

    public async Task Cancel()
    {
        if (ActionCompleted is not null)
            await ActionCompleted();
    }
    private async Task<IEnumerable<string>> Search(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value))
            return _expenseTypes;

        var result = _expenseTypes.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
        return await Task.FromResult(result);
    }
}