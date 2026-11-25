using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components.Pages;

/// <summary>
/// Every cash, bond and investment transaction of the user in one newest-first list, grouped by day. The list is read
/// page by page through the transaction log cursor; changing the date range or the kind or currency filters starts
/// again from the newest entry of the new selection. Each entry renders with the account pages' own row
/// (details, labels, valuation, edit and delete); a successful edit or delete reloads from the first page.
/// </summary>
public partial class TransactionsPage : ComponentBase, IAsyncDisposable
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

    private IReadOnlyList<string> _currencies = [];
    private TransactionLogFilter _filter = TransactionLogFilter.None;

    private List<TransactionHistoryItem> _entries = [];
    private List<DayGroup> _dayGroups = [];
    private TransactionLogCursor? _nextCursor;
    private bool _isLoading = true;
    private bool _isLoadingMore;
    private bool _hasError;
    private bool _loadMoreFailed;

    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;

    // Row enrichment loaded after a page paints: bond definitions (shared across pages) and investment
    // valuations. A reset bumps the generation so replies for the previous selection are dropped.
    private BondDetailsRequestLoader? _bondDetailsLoader;
    private readonly Dictionary<int, BondDetails> _bondDetails = [];
    private readonly Dictionary<long, InvestmentTransactionValuationDto> _valuations = [];
    private CancellationTokenSource _enrichmentCts = new();
    private int _enrichmentGeneration;

    private InvestmentTransactionDto? _editingInvestment;
    private bool _investmentFormVisible;

    /// <summary><c>?search=</c> pre-fills the text search, so a filtered view can be linked to directly.</summary>
    [SupplyParameterFromQuery(Name = "search")] public string? Search { get; set; }

    /// <summary><c>?range=</c> picks one of the toolbar's preset ranges (e.g. 1M, YTD); anything else keeps the default.</summary>
    [SupplyParameterFromQuery(Name = "range")] public string? Range { get; set; }

    [Inject] public required ILogger<TransactionsPage> Logger { get; set; }
    [Inject] public required TransactionLogHttpClient TransactionLogHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required InvestmentValuationHttpClient InvestmentValuationHttpClient { get; set; }
    [Inject] public required InvestmentTransactionHttpClient InvestmentTransactionHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IBrowserViewportService BrowserViewportService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrWhiteSpace(Search)) _search = Search.Trim();
        if (TransactionLogToolbar.Ranges.FirstOrDefault(range => range.Equals(Range, StringComparison.OrdinalIgnoreCase)) is string preset)
            _selectedRange = preset;
        ApplyRange();
        // Rows publish this after a successful cash or bond edit/delete; the list then starts over so moved
        // or removed entries show up. Cancelled edits do not publish.
        AccountDataSynchronizationService.AccountsChanged += OnAccountsChanged;
        var user = await LoginService.GetLoggedUser();
        if (_disposed) return;

        _userId = user?.UserId;
        // The currency list only feeds the filter options, so it must not delay (or fail) the first page.
        var currenciesTask = LoadCurrenciesAsync(user?.UserId);
        await LoadAsync(reset: true);
        await currenciesTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Same breakpoint as the account pages, so the rows lay out as they do there.
        await BrowserViewportService.SubscribeAsync(_viewportSubscriptionId, async args =>
        {
            var isMobile = args.Breakpoint is Breakpoint.Xs or Breakpoint.Sm or Breakpoint.Md;
            if (isMobile == _isMobile) return;
            _isMobile = isMobile;
            await InvokeAsync(StateHasChanged);
        }, fireImmediately: true);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        AccountDataSynchronizationService.AccountsChanged -= OnAccountsChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _enrichmentCts.Cancel();
        _enrichmentCts.Dispose();
        try
        {
            await BrowserViewportService.UnsubscribeAsync(_viewportSubscriptionId);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to unsubscribe from BrowserViewportService");
        }
    }

    private void OnAccountsChanged() => _ = InvokeAsync(() => _disposed ? Task.CompletedTask : Task.WhenAll(LoadAsync(reset: true), LoadCurrenciesAsync(_userId)));

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

    // The filter offers only the currencies the user's own transactions are in.
    private async Task LoadCurrenciesAsync(int? userId)
    {
        if (userId is not int id) return;
        try
        {
            var currencies = await TransactionLogHttpClient.GetCurrencies(id);
            if (_disposed) return;
            _currencies = currencies;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            // Without the list the currency select stays hidden; the kind chips and the log itself still work.
            Logger.LogError(ex, "Failed to load the currencies for the transaction filters.");
        }
    }

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
        // A save or delete can finish after the user left the page; there is nothing left to reload.
        if (_disposed) return;
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
            ResetEnrichment();
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
            TransactionHistoryPage? page = null;
            if (_userId is int userId)
                page = await TransactionLogHttpClient.GetItemsPage(userId, new TransactionLogQuery(_rangeStart, _rangeEnd, PageSize, cursor, _filter.Kinds, _filter.Currencies, _search), cts.Token);
            if (_disposed || !_loadGate.IsCurrent(version)) return;

            if (page is not null)
            {
                _entries = [.. _entries, .. page.Items];
                _dayGroups = BuildDayGroups(_entries);
                _nextCursor = page.NextCursor;
                _ = EnrichAsync(page.Items);
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

    internal static List<DayGroup> BuildDayGroups(IEnumerable<TransactionHistoryItem> entries) =>
    [
        .. entries.GroupBy(entry => entry.Date.Date).Select(day =>
        {
            List<TransactionHistoryItem> items = [.. day];
            // As on the cash account page: a time every cash row of the day shares says nothing, so it is hidden.
            var cashTimes = items.OfType<CurrencyTransactionHistoryItem>().Select(item => item.Entry.PostingDate.ToLocalTime()).ToList();
            return new DayGroup(day.Key, items, cashTimes.Count > 1 && TransactionRowPresentation.AllShareSameTime(cashTimes));
        })
    ];

    private void ResetEnrichment()
    {
        _enrichmentGeneration++;
        _enrichmentCts.Cancel();
        _enrichmentCts.Dispose();
        _enrichmentCts = new CancellationTokenSource();
        _valuations.Clear();
    }

    // Loads what the rows need beyond the feed, after the page has painted: bond definitions for the bond
    // valuation details and, per investment account, the valuations of its purchases (the only trades the
    // row values). Failures leave the rows on their fallbacks, exactly as on the account pages.
    private async Task EnrichAsync(IReadOnlyList<TransactionHistoryItem> items)
    {
        var generation = _enrichmentGeneration;
        var token = _enrichmentCts.Token;

        var bondDetailsIds = items.OfType<BondTransactionHistoryItem>().Select(item => item.Entry.BondDetailsId)
            .Distinct().Where(id => !_bondDetails.ContainsKey(id)).ToList();
        var buysByAccount = items.OfType<InvestmentTransactionHistoryItem>()
            .Where(item => item.LegacyAccountId.HasValue && item.Transaction.Type == InvestmentTransactionTypeDto.Buy)
            .GroupBy(item => item.LegacyAccountId!.Value, item => item.Transaction.Id)
            .ToList();
        if (bondDetailsIds.Count == 0 && buysByAccount.Count == 0) return;

        var bondTask = LoadBondDetailsAsync(bondDetailsIds);
        var valuationTask = LoadValuationsAsync(buysByAccount, token);
        await Task.WhenAll(bondTask, valuationTask);

        if (_disposed) return;
        foreach (var details in bondTask.Result)
            _bondDetails[details.Id] = details;
        if (generation == _enrichmentGeneration)
            foreach (var valuation in valuationTask.Result)
                _valuations[valuation.TransactionId] = valuation;
        StateHasChanged();
    }

    // Each definition loads on its own, so one failed lookup leaves the other bond rows' details intact.
    private async Task<List<BondDetails>> LoadBondDetailsAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return [];
        var loader = _bondDetailsLoader ??= new(id => BondDetailsHttpClient.GetById(id));
        var details = await Task.WhenAll(ids.Select(async id =>
        {
            try
            {
                return await loader.LoadAsync(id);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load bond details {BondDetailsId} for the transactions page.", id);
                return null;
            }
        }));
        return [.. details.OfType<BondDetails>()];
    }

    private async Task<List<InvestmentTransactionValuationDto>> LoadValuationsAsync(
        IReadOnlyList<IGrouping<int, long>> buysByAccount, CancellationToken cancellationToken)
    {
        if (buysByAccount.Count == 0) return [];
        try
        {
            var currency = await SettingsService.GetCurrencyAsync();
            var batches = await Task.WhenAll(buysByAccount.Select(async account =>
            {
                try
                {
                    return await InvestmentValuationHttpClient.GetTransactionValuationsAsync(account.Key, currency.Id, [.. account], cancellationToken);
                }
                // Per account, so one failing or timed-out account keeps the others' valuations; only a
                // cancellation of this enrichment (a reload or disposal) abandons the whole batch.
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    Logger.LogWarning(ex, "Failed to load transaction valuations for an investment account.");
                    return [];
                }
            }));
            return [.. batches.SelectMany(batch => batch)];
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to resolve the valuation currency for the transactions page.");
            return [];
        }
    }

    private void ShowInvestmentEdit(InvestmentTransactionDto transaction)
    {
        if (transaction.AccountId is null) return;
        _editingInvestment = transaction;
        _investmentFormVisible = true;
    }

    private Task OnInvestmentFormVisibleChanged(bool visible)
    {
        _investmentFormVisible = visible;
        if (!visible) _editingInvestment = null;
        return Task.CompletedTask;
    }

    private Task OnInvestmentSavedAsync()
    {
        _investmentFormVisible = false;
        _editingInvestment = null;
        return LoadAsync(reset: true);
    }

    private async Task DeleteInvestmentAsync(InvestmentTransactionDto transaction)
    {
        if (transaction.AccountId is not int accountId) return;
        try
        {
            if (await InvestmentTransactionHttpClient.DeleteAsync(accountId, transaction.Id))
            {
                Snackbar.Add("Transaction removed.", Severity.Success);
                await LoadAsync(reset: true);
            }
            else
            {
                Snackbar.Add("Could not remove the transaction.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete investment transaction {TransactionId}", transaction.Id);
            Snackbar.Add("Could not remove the transaction.", Severity.Error);
        }
    }

    internal sealed record DayGroup(DateTime Day, IReadOnlyList<TransactionHistoryItem> Items, bool CashSiblingsShareSameTime);

    private string EmptyMessage => _filter.IsActive || !string.IsNullOrWhiteSpace(_search)
        ? $"No transactions match the selected filters between {DateFormatter.Format(_rangeStart)} and {DateFormatter.Format(_rangeEnd)}. Adjust the filters or pick a longer date range."
        : $"No transactions between {DateFormatter.Format(_rangeStart)} and {DateFormatter.Format(_rangeEnd)}. Pick a longer date range to look further back.";
}