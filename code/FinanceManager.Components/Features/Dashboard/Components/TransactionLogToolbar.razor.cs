using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components;

public partial class TransactionLogToolbar : ComponentBase
{
    /// <summary>Preset ranges offered by the dropdown: the same ones the account details pages use.</summary>
    public static readonly string[] Ranges = AccountHistoryToolbar.Ranges;

    private static readonly (AccountType Type, string Label)[] _typeOptions =
    [
        (AccountType.Currency, "Cash"),
        (AccountType.Bond, "Bonds"),
        (AccountType.Stock, "Investments"),
    ];

    private MudMenu? _rangeMenu;
    private MudDateRangePicker? _customDateRangePicker;

    [Parameter] public string SelectedRange { get; set; } = "3M";
    [Parameter] public EventCallback<string> SelectedRangeChanged { get; set; }
    [Parameter] public DateRange? CustomDateRange { get; set; }
    [Parameter] public EventCallback<DateRange?> CustomDateRangeChanged { get; set; }

    /// <summary>The user's accounts, offered by the account select.</summary>
    [Parameter] public IReadOnlyList<TransactionLogAccountOption> Accounts { get; set; } = [];
    [Parameter] public TransactionLogFilter Filter { get; set; } = TransactionLogFilter.None;
    [Parameter] public EventCallback<TransactionLogFilter> FilterChanged { get; set; }

    /// <summary>Accounts offered by the select: only those of the selected types, or all when no type is selected.</summary>
    private IReadOnlyList<TransactionLogAccountOption> VisibleAccounts =>
        [.. Accounts
            .Where(account => Filter.AccountTypes.Count == 0 || Filter.AccountTypes.Contains(account.AccountType))
            .OrderBy(account => account.AccountName, StringComparer.CurrentCultureIgnoreCase)];

    private bool IsCustomRange => SelectedRange == DateRangeHelper.CustomRangeKey;

    /// <summary>Label on the dropdown: the preset key, or the picked days for a custom range.</summary>
    private string RangeButtonText =>
        IsCustomRange && CustomDateRange is { Start: DateTime start, End: DateTime end }
            ? DateFormatter.FormatRange(start, end)
            : SelectedRange;

    private string RangeButtonAccessibleText =>
        IsCustomRange && CustomDateRange is { Start: DateTime start, End: DateTime end }
            ? $"Date range: {DateFormatter.Format(start)} to {DateFormatter.Format(end)}"
            : $"Date range: {SelectedRange}";

    private async Task ToggleRangeMenu(MouseEventArgs args)
    {
        if (_rangeMenu is not null)
            await _rangeMenu.ToggleMenuAsync(args);
    }

    private async Task OnRangeSelected(string value)
    {
        SelectedRange = value;
        if (_rangeMenu is not null)
            await _rangeMenu.CloseMenuAsync();
        await SelectedRangeChanged.InvokeAsync(value);
    }

    private async Task OpenCustomDateRangePicker()
    {
        if (_rangeMenu is not null)
            await _rangeMenu.CloseMenuAsync();
        if (_customDateRangePicker is not null)
            await _customDateRangePicker.OpenAsync();
    }

    private async Task OnCustomDateRangeChanged(DateRange? value)
    {
        CustomDateRange = value;
        SelectedRange = DateRangeHelper.CustomRangeKey;
        if (value is { Start: DateTime, End: DateTime } && _customDateRangePicker is not null)
            await _customDateRangePicker.CloseAsync(submit: true);
        await CustomDateRangeChanged.InvokeAsync(value);
    }

    private string GetAccountName(int accountId) =>
        Accounts.FirstOrDefault(account => account.AccountId == accountId)?.AccountName ?? string.Empty;

    private static string GetAccountsSummary(IReadOnlyList<string> selectedNames) => selectedNames.Count switch
    {
        0 => string.Empty,
        1 => selectedNames[0],
        _ => $"{selectedNames.Count} accounts",
    };

    private async Task OnAccountTypesChanged(IReadOnlyCollection<AccountType>? selected)
    {
        var types = (selected ?? []).Distinct().Order().ToList();
        // Narrowing the types hides accounts of the other types, so they stop being part of the filter.
        var visibleIds = Accounts
            .Where(account => types.Count == 0 || types.Contains(account.AccountType))
            .Select(account => account.AccountId)
            .ToHashSet();
        var accountIds = Filter.AccountIds.Where(visibleIds.Contains).ToList();
        await FilterChanged.InvokeAsync(new TransactionLogFilter(types, accountIds));
    }

    private async Task OnAccountIdsChanged(IEnumerable<int>? selected) =>
        await FilterChanged.InvokeAsync(new TransactionLogFilter(Filter.AccountTypes, [.. (selected ?? []).Distinct()]));
}