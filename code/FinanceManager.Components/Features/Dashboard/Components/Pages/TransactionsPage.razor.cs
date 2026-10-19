using FinanceManager.Components.Features.Dashboard.Components;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components.Pages;

/// <summary>
/// Every transaction of the user's accounts in one newest-first list, grouped by day. The list is read
/// page by page through the transaction log cursor; changing the date range starts again from the newest
/// entry of the new range.
/// </summary>
public partial class TransactionsPage : ComponentBase, IDisposable
{
    internal const int PageSize = 25;
    private const string _defaultRange = "3M";

    private readonly RefreshVersionGate _loadGate = new();
    private CancellationTokenSource? _loadCts;
    private int? _userId;
    private bool _disposed;

    private string _selectedRange = _defaultRange;
    private DateRange? _customRange;
    private DateTime _rangeStart;
    private DateTime _rangeEnd;

    private List<TransactionLogEntryDto> _entries = [];
    private List<IGrouping<DateTime, TransactionLogEntryDto>> _dayGroups = [];
    private TransactionLogCursor? _nextCursor;
    private bool _isLoading = true;
    private bool _isLoadingMore;
    private bool _hasError;
    private bool _loadMoreFailed;

    [Inject] public required ILogger<TransactionsPage> Logger { get; set; }
    [Inject] public required TransactionLogHttpClient TransactionLogHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ApplyRange();
        var user = await LoginService.GetLoggedUser();
        if (_disposed) return;

        _userId = user?.UserId;
        await LoadAsync(reset: true);
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
                page = await TransactionLogHttpClient.GetPage(userId, new TransactionLogQuery(_rangeStart, _rangeEnd, PageSize, cursor), cts.Token);
            if (_disposed || !_loadGate.IsCurrent(version)) return;

            if (page is not null)
            {
                _entries = [.. _entries, .. page.Items];
                _dayGroups = [.. _entries.GroupBy(entry => entry.Date.Date)];
                _nextCursor = page.NextCursor;
            }
        }
        catch (OperationCanceledException)
        {
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

    private string EmptyRangeMessage => $"No transactions between {DateFormatter.Format(_rangeStart)} and {DateFormatter.Format(_rangeEnd)}.";
}