using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components.Pages;

/// <summary>
/// Every transaction of the user's accounts in one newest-first list, grouped by day. The list is read
/// page by page through the transaction log cursor; changing the date range or the account filters starts
/// again from the newest entry of the new selection.
/// </summary>
public partial class TransactionsPage : ComponentBase, IDisposable
{
    public const int PageSize = 25;
    private const string _defaultRange = "3M";

    private readonly RefreshVersionGate _loadGate = new();
    private CancellationTokenSource? _loadCts;
    private int? _userId;
    private bool _disposed;

    private string? _search;
    private string _selectedRange = _defaultRange;
    private DateRange? _customRange;
    private DateTime _rangeStart;
    private DateTime _rangeEnd;

    private IReadOnlyList<TransactionLogAccountOption> _accounts = [];
    private TransactionLogFilter _filter = TransactionLogFilter.None;

    private List<TransactionLogEntryDto> _entries = [];
    private List<IGrouping<DateTime, TransactionLogEntryDto>> _dayGroups = [];
    private TransactionLogCursor? _nextCursor;
    private bool _isLoading = true;
    private bool _isLoadingMore;
    private bool _hasError;
    private bool _loadMoreFailed;

    /// <summary><c>?search=</c> pre-fills the text search, so a filtered view can be linked to directly.</summary>
    [SupplyParameterFromQuery(Name = "search")] public string? Search { get; set; }

    /// <summary><c>?range=</c> picks one of the toolbar's preset ranges (e.g. 1M, YTD); anything else keeps the default.</summary>
    [SupplyParameterFromQuery(Name = "range")] public string? Range { get; set; }

    [Inject] public required ILogger<TransactionsPage> Logger { get; set; }
    [Inject] public required TransactionLogHttpClient TransactionLogHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrWhiteSpace(Search)) _search = Search.Trim();
        if (TransactionLogToolbar.Ranges.FirstOrDefault(range => range.Equals(Range, StringComparison.OrdinalIgnoreCase)) is string preset)
            _selectedRange = preset;
        ApplyRange();
        var user = await LoginService.GetLoggedUser();
        if (_disposed) return;

        _userId = user?.UserId;
        // The account list only feeds the filter options, so it must not delay (or fail) the first page.
        var accountsTask = LoadAccountsAsync();
        await LoadAsync(reset: true);
        await accountsTask;
    }

    public void Dispose()
    {
        _disposed = true;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }

    private Task OnRangeChanged(string range)
    {
        _selectedRange = range;
        ApplyRange();
        return LoadAsync(reset: true);
    }

    private Task OnCustomRangeChanged(DateRange? range)
    {
        _customRange = range;
        _selectedRange = DateRangeHelper.CustomRangeKey;
        ApplyRange();
        return LoadAsync(reset: true);
    }

    private Task OnFilterChanged(TransactionLogFilter filter)
    {
        _filter = filter;
        return LoadAsync(reset: true);
    }

    private Task OnSearchChanged(string? search)
    {
        _search = search;
        return LoadAsync(reset: true);
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var types = await FinancialAccountService.GetAvailableAccounts();
            var names = await FinancialAccountService.GetAvailableAccountNames();
            if (_disposed) return;

            _accounts =
            [
                .. types.Select(account => (account.Key, Type: ToAccountType(account.Value)))
                    .Where(account => account.Type.HasValue && names.ContainsKey(account.Key))
                    .Select(account => new TransactionLogAccountOption(account.Key, names[account.Key], account.Type!.Value))
            ];
            StateHasChanged();
        }
        catch (Exception ex)
        {
            // Without the list the account select stays disabled; the type chips and the log itself still work.
            Logger.LogError(ex, "Failed to load the accounts for the transaction filters.");
        }
    }

    private static AccountType? ToAccountType(Type accountType) =>
        accountType == typeof(CurrencyAccount) ? AccountType.Currency
        : accountType == typeof(BondAccount) ? AccountType.Bond
        : accountType == typeof(InvestmentAccount) ? AccountType.Stock
        : null;

    private void ApplyRange()
    {
        var now = DateTime.UtcNow;
        var (start, end) = DateRangeHelper.GetAccountDetailsRange(
            _selectedRange, _customRange?.Start, _customRange?.End, now.AddMonths(-3), now.AddMonths(-3), now);

        // Whole days from the start day on, and explicitly UTC so the endpoint reads them the same way.
        start = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end, DateTimeKind.Utc);
        _rangeStart = start > end ? DateTime.SpecifyKind(end.Date, DateTimeKind.Utc) : start;
        _rangeEnd = end;
    }

    private Task Retry() => LoadAsync(reset: true);

    private Task LoadMore() => LoadAsync(reset: false);

    private async Task LoadAsync(bool reset)
    {
        if (!reset && (_isLoadingMore || _nextCursor is null)) return;

        // A newer load (a range change, a retry) supersedes whatever is in flight: its request is
        // cancelled and, should its response still arrive, the version gate keeps it from being shown.
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = _loadCts = new CancellationTokenSource();
        var version = _loadGate.Claim();

        var cursor = reset ? null : _nextCursor;
        if (reset)
        {
            _entries = [];
            _dayGroups = [];
            _nextCursor = null;
            _isLoading = true;
            _isLoadingMore = false;
        }
        else
        {
            _isLoadingMore = true;
        }
        _hasError = false;
        _loadMoreFailed = false;
        StateHasChanged();

        try
        {
            TransactionLogPage? page = null;
            if (_userId is int userId)
                page = await TransactionLogHttpClient.GetPage(userId, new TransactionLogQuery(_rangeStart, _rangeEnd, PageSize, cursor, _filter.AccountTypes, _filter.AccountIds, _search), cts.Token);
            if (_disposed || !_loadGate.IsCurrent(version)) return;

            if (page is not null)
            {
                _entries = [.. _entries, .. page.Items];
                _dayGroups = [.. _entries.GroupBy(entry => entry.Date.Date)];
                _nextCursor = page.NextCursor;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Cancelled by a newer load (or disposal), which now owns the loading state. An HttpClient
            // timeout also surfaces as a cancellation but is a failure, so it falls through to the error path.
            return;
        }
        catch (Exception ex)
        {
            if (_disposed || !_loadGate.IsCurrent(version)) return;

            Logger.LogError(ex, "Failed to load the transactions page.");
            // A failed first page replaces the list with the error state; a failed later page keeps
            // what is already shown and offers a retry below it.
            _hasError = reset;
            _loadMoreFailed = !reset;
        }

        _isLoading = false;
        _isLoadingMore = false;
        StateHasChanged();
    }

    private string EmptyMessage => _filter.IsActive || !string.IsNullOrWhiteSpace(_search)
        ? $"No transactions match the selected filters between {DateFormatter.Format(_rangeStart)} and {DateFormatter.Format(_rangeEnd)}. Adjust the filters or pick a longer date range."
        : $"No transactions between {DateFormatter.Format(_rangeStart)} and {DateFormatter.Format(_rangeEnd)}. Pick a longer date range to look further back.";
}