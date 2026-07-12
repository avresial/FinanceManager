using FinanceManager.Components.Components.Features.FinancialAccounts.Shared;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.InvestmentAccountComponents;

public partial class InvestmentAccountDetailsPageContent : ComponentBase, IAsyncDisposable
{
    [Parameter] public required int AccountId { get; set; }

    [Inject] public required InvestmentTransactionHttpClient TransactionHttpClient { get; set; }
    [Inject] public required InvestmentValuationHttpClient ValuationHttpClient { get; set; }
    [Inject] public required InvestmentAccountHttpClient InvestmentAccountHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IBrowserViewportService BrowserViewportService { get; set; }
    [Inject] public required ILogger<InvestmentAccountDetailsPageContent> Logger { get; set; }

    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _insightsDrawerOpen;
    private bool _isLoading = true;
    private int? _loadedAccountId;
    private string _accountName = "Investments";
    private readonly string _accountTypeLabel = "Investment account";
    private Currency _currency = DefaultCurrency.USD;
    private List<InvestmentTransactionDto> _transactions = [];
    private List<HoldingRow> _holdings = [];

    // Range / chart state.
    private string _selectedRange = "3M";
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateRange? _customDateRange;
    private bool _isChartLoading;
    private int _chartRefreshVersion;
    private decimal _currentBalance;
    private decimal _balanceChange;
    private decimal? _balanceChangePercent;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    // Toolbar filter state.
    private string? _searchText;
    private AccountHistoryToolbar.TxFilter? _activeFilter;
    private List<InvestmentTransactionDto>? _top5;
    private List<InvestmentTransactionDto>? _bottom5;

    // Add/edit overlay state.
    private bool _formVisible;
    private long? _editingId;
    private InstrumentSearchResultDto? _selectedInstrument;
    private long _formListingId;
    private InvestmentTransactionType _formType = InvestmentTransactionType.Buy;
    private decimal _formQuantity = 1m;
    private decimal _formUnitPrice;
    private string _formCurrency = string.Empty;
    private DateTime? _formTradeDate = DateTime.Today;
    private decimal? _formFee;
    private string? _formNotes;
    private bool _saving;
    private bool _noPriceAvailable;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedAccountId == AccountId) return;
        SetDateRangeForSelection();
        await LoadAsync(initialLoad: true);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await BrowserViewportService.SubscribeAsync(_viewportSubscriptionId, async args =>
        {
            var isMobile = args.Breakpoint is Breakpoint.Xs or Breakpoint.Sm or Breakpoint.Md;
            if (isMobile == _isMobile) return;
            _isMobile = isMobile;
            if (!_isMobile) _insightsDrawerOpen = false;
            await InvokeAsync(StateHasChanged);
        }, fireImmediately: true);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await BrowserViewportService.UnsubscribeAsync(_viewportSubscriptionId);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to unsubscribe from BrowserViewportService");
        }
        GC.SuppressFinalize(this);
    }

    private void ToggleInsightsDrawer() => _insightsDrawerOpen = !_insightsDrawerOpen;

    private void CloseInsightsDrawer() => _insightsDrawerOpen = false;

    private async Task LoadAsync(bool initialLoad = false)
    {
        _isLoading = true;
        _currency = await SettingsService.GetCurrencyAsync();
        try
        {
            _accountName = (await InvestmentAccountHttpClient.GetAccountAsync(AccountId))?.Name ?? "Investments";
            _transactions = [.. await TransactionHttpClient.GetByAccountAsync(AccountId)];
            _loadedAccountId = AccountId;

            if (initialLoad)
                ApplyAutomaticCustomRange();

            await UpdateInfo();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load investment account {AccountId}", AccountId);
            Snackbar.Add("Could not load the investment account.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    // Recomputes the in-range movers and, unless suppressed, queues a fresh chart + holdings refresh.
    private Task UpdateInfo(bool refreshChart = true)
    {
        _currency = SettingsService.GetCurrency();

        var filtered = GetFilteredTransactions();
        var ordered = filtered.OrderByDescending(CashImpact).ToList();
        _top5 = [.. ordered.Where(t => CashImpact(t) > 0).Take(5)];
        _bottom5 = [.. ordered.Where(t => CashImpact(t) < 0).OrderBy(CashImpact).Take(5)];

        if (refreshChart)
            QueueChartDataRefresh();

        return Task.CompletedTask;
    }

    private void QueueChartDataRefresh()
    {
        var refreshVersion = ++_chartRefreshVersion;
        // Snapshot the request inputs so an in-flight refresh keeps using the account/range it
        // was started with, even if a newer range/account is selected before it completes.
        var accountId = AccountId;
        var currency = _currency;
        var dateStart = _dateStart;
        var dateEnd = _dateEnd;
        _isChartLoading = true;
        ChartData.Clear();

        _ = InvokeAsync(async () =>
        {
            try
            {
                await UpdateChartData(refreshVersion, accountId, currency, dateStart, dateEnd);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while loading investment account chart data for account ID {AccountId}", AccountId);
            }
            finally
            {
                if (refreshVersion == _chartRefreshVersion)
                    _isChartLoading = false;

                StateHasChanged();
            }
        });
    }

    private async Task UpdateChartData(int refreshVersion, int accountId, Currency currency, DateTime dateStart, DateTime dateEnd)
    {
        var series = await ValuationHttpClient.GetValueSeriesAsync(accountId, currency.Id, dateStart, dateEnd);
        var holdings = await ValuationHttpClient.GetHoldingsAsync(accountId, dateEnd);
        if (refreshVersion != _chartRefreshVersion || accountId != AccountId) return;

        // Keep the full ordered series for balance maths; trim only leading zeros for the chart so
        // a range that starts before the first holding still reports the true change from zero.
        var orderedSeries = series
            .OrderBy(kv => kv.Key)
            .Select(kv => new TimeSeriesModel(kv.Key, kv.Value))
            .ToList();

        ChartData.Clear();
        ChartData.AddRange(orderedSeries.SkipWhile(x => x.Value == 0));
        UpdateBalanceFromChartData(orderedSeries);
        UpdateHoldings(holdings, dateEnd);
    }

    private void UpdateBalanceFromChartData(IReadOnlyList<TimeSeriesModel> balanceSeries)
    {
        _currentBalance = balanceSeries.LastOrDefault()?.Value ?? 0;
        _balanceChange = balanceSeries.Count >= 2 ? balanceSeries[^1].Value - balanceSeries[0].Value : 0;

        var startBalance = _currentBalance - _balanceChange;
        _balanceChangePercent = startBalance == 0 ? null : _balanceChange / startBalance * 100m;
    }

    private void UpdateHoldings(IReadOnlyDictionary<long, decimal> holdings, DateTime asOf)
    {
        // Value each holding from its latest trade on or before the as-of date so historical
        // ranges don't pull ticker/price metadata from trades that happen after the range end.
        var asOfDate = DateOnly.FromDateTime(asOf);
        var latestByListing = _transactions
            .Where(t => t.TradeDate <= asOfDate)
            .GroupBy(t => t.AssetListingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.Id).First());

        var rows = new List<HoldingRow>();
        foreach (var (listingId, quantity) in holdings)
        {
            if (quantity == 0m || !latestByListing.TryGetValue(listingId, out var latest)) continue;
            rows.Add(new HoldingRow(listingId, latest.Ticker, latest.ExchangeName, latest.Currency, quantity, latest.UnitPrice, quantity * latest.UnitPrice));
        }
        _holdings = [.. rows.OrderByDescending(h => h.Value)];
    }

    private bool HasActiveFilter => !string.IsNullOrWhiteSpace(_searchText) || _activeFilter is not null;

    private List<InvestmentTransactionDto> GetFilteredTransactions()
    {
        IEnumerable<InvestmentTransactionDto> transactions = _transactions
            .Where(t => t.TradeDate >= DateOnly.FromDateTime(_dateStart) && t.TradeDate <= DateOnly.FromDateTime(_dateEnd));

        // Income/Expense maps to the cash direction of the trade: a sell brings cash in, a buy takes it out.
        if (_activeFilter == AccountHistoryToolbar.TxFilter.Income)
            transactions = transactions.Where(t => t.Type == InvestmentTransactionType.Sell);
        else if (_activeFilter == AccountHistoryToolbar.TxFilter.Expense)
            transactions = transactions.Where(t => t.Type == InvestmentTransactionType.Buy);

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var needle = _searchText.Trim();
            transactions = transactions.Where(t =>
                t.Ticker.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || t.ExchangeName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (t.Notes is not null && t.Notes.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. transactions
            .OrderByDescending(t => t.TradeDate)
            .ThenByDescending(t => t.Id)];
    }

    private async Task OnRangeChanged(string value)
    {
        _selectedRange = value;
        SetDateRangeForSelection();
        await UpdateInfo();
        StateHasChanged();
    }

    private async Task OnCustomDateRangeChanged(DateRange? range)
    {
        _customDateRange = range;
        _selectedRange = AccountDetailsHero.CustomRangeKey;
        SetDateRangeForSelection();
        await UpdateInfo();
        StateHasChanged();
    }

    private async Task OnSearchChanged(string? value)
    {
        _searchText = value;
        await UpdateInfo(refreshChart: false);
        StateHasChanged();
    }

    private async Task OnTxFilterChanged(AccountHistoryToolbar.TxFilter? value)
    {
        _activeFilter = value;
        await UpdateInfo(refreshChart: false);
        StateHasChanged();
    }

    private void SetDateRangeForSelection()
    {
        var today = DateTime.UtcNow;
        if (_selectedRange == AccountDetailsHero.CustomRangeKey)
        {
            _dateStart = _customDateRange?.Start ?? today.AddMonths(-3);
            _dateEnd = _customDateRange?.End ?? today;
            if (_dateEnd > today)
                _dateEnd = today;
            return;
        }

        _dateStart = _selectedRange switch
        {
            "Month" => DateRangeHelper.GetCurrentMonthRange().Start,
            "1M" => today.AddMonths(-1),
            "3M" => today.AddMonths(-3),
            "6M" => today.AddMonths(-6),
            "YTD" => new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => today.AddMonths(-3)
        };
        _dateEnd = today;
    }

    // On the first load, widen the range to the oldest recorded trade when it predates the
    // default window, so navigating in doesn't hide existing history behind an empty range.
    private void ApplyAutomaticCustomRange()
    {
        var oldestTradeDate = _transactions.MinBy(t => t.TradeDate)?.TradeDate;
        if (oldestTradeDate is null) return;

        var oldestStart = oldestTradeDate.Value.ToDateTime(TimeOnly.MinValue);
        if (oldestStart >= _dateStart) return;

        _selectedRange = AccountDetailsHero.CustomRangeKey;
        _dateStart = oldestStart;
        _customDateRange = new DateRange(_dateStart, _dateEnd);
    }

    private static decimal CashImpact(InvestmentTransactionDto t)
    {
        var gross = t.Quantity * t.UnitPrice;
        var fee = t.Fee ?? 0m;
        return t.Type == InvestmentTransactionType.Sell ? gross - fee : -(gross + fee);
    }

    private void ShowAdd()
    {
        _editingId = null;
        _selectedInstrument = null;
        _formListingId = 0;
        _formType = InvestmentTransactionType.Buy;
        _formQuantity = 1m;
        _formUnitPrice = 0m;
        _formCurrency = string.Empty;
        _formTradeDate = DateTime.Today;
        _formFee = null;
        _formNotes = null;
        _noPriceAvailable = false;
        _formVisible = true;
    }

    private void ShowEdit(InvestmentTransactionDto tx)
    {
        _editingId = tx.Id;
        _selectedInstrument = new InstrumentSearchResultDto(tx.AssetListingId, tx.Ticker, tx.ExchangeName, tx.Currency);
        _formListingId = tx.AssetListingId;
        _formType = tx.Type;
        _formQuantity = tx.Quantity;
        _formUnitPrice = tx.UnitPrice;
        _formCurrency = tx.Currency;
        _formTradeDate = tx.TradeDate.ToDateTime(TimeOnly.MinValue);
        _formFee = tx.Fee;
        _formNotes = tx.Notes;
        _formVisible = true;
    }

    private void CloseForm() => _formVisible = false;

    private bool CanSave => _formListingId > 0 && _formQuantity > 0 && _formUnitPrice >= 0 && _formTradeDate is not null && !string.IsNullOrWhiteSpace(_formCurrency);

    private async Task OnInstrumentSelectedAsync(InstrumentSearchResultDto? dto)
    {
        _selectedInstrument = dto;
        _noPriceAvailable = false;
        if (dto is null)
        {
            _formListingId = 0;
            _formCurrency = string.Empty;
            _formUnitPrice = 0m;
            return;
        }

        _formListingId = dto.ListingId;
        _formCurrency = dto.Currency;

        if (_editingId is null)
        {
            try
            {
                var priceInfo = await TransactionHttpClient.GetListingPriceAsync(dto.ListingId);
                if (priceInfo?.LatestPrice is decimal price)
                {
                    _formUnitPrice = price;
                }
                else
                {
                    _formUnitPrice = 0m;
                    _noPriceAvailable = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to fetch price for listing {ListingId}", dto.ListingId);
                _formUnitPrice = 0m;
                _noPriceAvailable = true;
            }
        }
    }

    private async Task<IEnumerable<InstrumentSearchResultDto>> SearchInstrumentsAsync(string value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return await TransactionHttpClient.SearchListingsAsync(value);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search instrument listings for '{Query}'", value);
            return [];
        }
    }

    private async Task OnInstrumentImportedAsync(ImportedInstrumentDto imported)
    {
        await OnInstrumentSelectedAsync(new InstrumentSearchResultDto(
            imported.AssetListingId, imported.Ticker, imported.ExchangeName, imported.TradingCurrency));
        Snackbar.Add(imported.Warnings.Count == 0 ? "Instrument imported." : $"Instrument imported with {imported.Warnings.Count} warning(s).", Severity.Success);
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        _saving = true;
        try
        {
            var tradeDate = DateOnly.FromDateTime(_formTradeDate!.Value);
            bool ok;
            if (_editingId is long id)
            {
                ok = await TransactionHttpClient.UpdateAsync(new UpdateInvestmentTransactionRequest(
                    id, AccountId, _formListingId, _formType, _formQuantity, _formUnitPrice, _formCurrency, tradeDate, _formFee, _formNotes));
            }
            else
            {
                var created = await TransactionHttpClient.AddAsync(new AddInvestmentTransactionRequest(
                    AccountId, _formListingId, _formType, _formQuantity, _formUnitPrice, _formCurrency, tradeDate, _formFee, _formNotes));
                ok = created is not null;
            }

            if (ok)
            {
                Snackbar.Add(_editingId is null ? "Transaction added." : "Transaction updated.", Severity.Success);
                _formVisible = false;
                await LoadAsync();
            }
            else
            {
                Snackbar.Add("Could not save the transaction.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save investment transaction for account {AccountId}", AccountId);
            Snackbar.Add("Could not save the transaction.", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync(InvestmentTransactionDto tx)
    {
        try
        {
            if (await TransactionHttpClient.DeleteAsync(AccountId, tx.Id))
            {
                Snackbar.Add("Transaction removed.", Severity.Success);
                await LoadAsync();
            }
            else
            {
                Snackbar.Add("Could not remove the transaction.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete investment transaction {TransactionId} for account {AccountId}", tx.Id, AccountId);
            Snackbar.Add("Could not remove the transaction.", Severity.Error);
        }
    }

    private sealed record HoldingRow(long ListingId, string Ticker, string ExchangeName, string Currency, decimal Quantity, decimal LatestPrice, decimal Value);
}