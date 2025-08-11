using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents;
public partial class UpdateBankEntry
{
    private int? loadedEntryId = null;
    private string _currency = string.Empty;
    private bool success;
    private string[] errors = [];
    private MudForm? form;

    private List<string> ExpenseTypes = Enum.GetValues(typeof(ExpenseType)).Cast<ExpenseType>().Select(x => x.ToString()).ToList();

    private TimeSpan? Time = new TimeSpan(01, 00, 00);
    private DateTime? PostingDate = DateTime.Today;
    private ExpenseType ExpenseType { get; set; } = FinanceManager.Domain.Enums.ExpenseType.Other;
    private string? Description { get; set; } = string.Empty;
    private decimal? BalanceChange { get; set; } = 0;
    private List<FinancialLabel> _labels { get; set; } = [];

    [Parameter] public Func<Task>? ActionCompleted { get; set; }

    [Parameter] public required BankAccount BankAccount { get; set; }

    [Parameter] public required BankAccountEntry BankAccountEntry { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

    protected override void OnParametersSet()
    {
        if (loadedEntryId.HasValue && loadedEntryId.Value == BankAccountEntry.EntryId) return;
        loadedEntryId = BankAccountEntry.EntryId;

        _currency = settingsService.GetCurrency();
        PostingDate = BankAccountEntry.PostingDate;
        Time = new TimeSpan(BankAccountEntry.PostingDate.Hour, BankAccountEntry.PostingDate.Minute, BankAccountEntry.PostingDate.Second);
        ExpenseType = BankAccountEntry.ExpenseType;
        Description = BankAccountEntry.Description;
        BalanceChange = BankAccountEntry.ValueChange;
        _labels = BankAccountEntry.Labels?.ToList() ?? [];
    }

    public async Task Update()
    {
        if (form is null) return;
        await form.Validate();

        if (!form.IsValid) return;
        if (!BalanceChange.HasValue) return;
        if (!PostingDate.HasValue) return;
        if (!Time.HasValue) return;

        DateTime date = new DateTime(PostingDate.Value.Year, PostingDate.Value.Month, PostingDate.Value.Day, Time.Value.Hours, Time.Value.Minutes, Time.Value.Seconds);
        BankAccountEntry bankAccountEntry = new(BankAccountEntry.AccountId, BankAccountEntry.EntryId, date, -1, BalanceChange.Value)
        {
            Description = this.Description is null ? string.Empty : this.Description,
            ExpenseType = ExpenseType,
            Labels = _labels
        };

        try
        {
            await FinancialAccountService.UpdateEntry(bankAccountEntry);
        }
        catch (Exception ex)
        {
            errors = [ex.ToString()];
        }

        if (errors.Length == 0)
        {
            await AccountDataSynchronizationService.AccountChanged();

            if (ActionCompleted is not null)
                await ActionCompleted();
        }
    }

    public async Task Cancel()
    {
        if (ActionCompleted is not null)
            await ActionCompleted();
    }

    private async Task<IEnumerable<string?>> Search(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value))
            return ExpenseTypes;

        return await Task.FromResult(ExpenseTypes.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }
}