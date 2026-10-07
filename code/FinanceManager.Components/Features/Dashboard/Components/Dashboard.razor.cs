using FinanceManager.Components.Features.Dashboard.HttpClients;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components;

public partial class Dashboard : ComponentBase
{
    private const int _unitHeight = 130;
    private static readonly string _halfCardHeight = $"{_unitHeight * 2}px";
    private static readonly string _thirdCardHeight = $"{_unitHeight * 3}px";

    private const int _third = 4;
    private const int _half = 6;
    private const int _full = 12;

    // Display order and large-screen widths of the cards in each height tier. Time-series cards
    // read well wide; distribution and list cards are capped so they are never stretched past
    // half width. See DashboardCardLayout for how a tier's visible cards fill rows.
    private static readonly TierCard[] _halfWidthCards =
    [
        new(DashboardCards.NetCashFlow, new(_half, _full)),
        new(DashboardCards.CashFlowForecast, new(_half, _full)),
        new(DashboardCards.ClosingBalance, new(_half, _full)),
    ];
    private static readonly TierCard[] _thirdWidthCards =
    [
        new(DashboardCards.Labels, new(_third, _half)),
        new(DashboardCards.Assets, new(_third, _half)),
        new(DashboardCards.Liabilities, new(_third, _half)),
        new(DashboardCards.Expenses, new(_third, _half)),
        new(DashboardCards.Insights, new(_third, _half)),
        new(DashboardCards.FinancialAlerts, new(_third, _half)),
        new(DashboardCards.RecurringTransactions, new(_third, _half)),
    ];

    private MudBlazor.MudMenu? _customizeMenu;

    private DashboardOverviewDto? _overview;
    private bool _isLoading = true;
    private bool _hasError;

    // The date range that the currently-held _overview was loaded for. Cards render
    // these (via DisplayStartDate/DisplayEndDate) rather than the live selection so
    // the displayed period label always matches the displayed amounts, even mid-reload.
    private DateTime _overviewStart;
    private DateTime _overviewEnd;

    // Guards against a slower, earlier reload overwriting a newer date selection:
    // every LoadOverview claims an incrementing version and only commits its result
    // if it is still the latest in-flight request.
    private readonly RefreshVersionGate _overviewGate = new();

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    // Dates handed to the cards: while an overview is held they track that overview's
    // range so amounts and period labels stay paired during a reload; with no overview
    // (first paint / self-load fallback) they fall back to the live selection.
    private DateTime DisplayStartDate => _overview is null ? StartDate : _overviewStart;
    private DateTime DisplayEndDate => _overview is null ? EndDate : _overviewEnd;

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required DashboardHttpClient DashboardHttpClient { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required DashboardCardVisibilityService CardVisibility { get; set; }

    // Card-specific models mapped from the single overview response. They are null
    // until the first load resolves (and when the overview is unavailable), in which
    // case the cards fall back to their existing standalone self-loading behavior.
    private TimeSeriesCardModel? NetWorthModel => _overview is null ? null : new(_overview.NetWorthSeries);
    private TimeSeriesCardModel? NetCashFlowModel => _overview is null ? null : new(_overview.NetCashFlowSeries);
    private TimeSeriesCardModel? ClosingBalanceModel => _overview is null ? null : new(_overview.ClosingBalanceSeries);
    private DistributionCardModel? LiabilitiesModel => _overview is null ? null : new(_overview.LiabilitiesPerType, _overview.LiabilitiesPerAccount);
    private NameValueListCardModel? LabelsModel => _overview is null ? null : new(_overview.LabelsValue);
    private DistributionCardModel? AssetsModel => _overview is null ? null : new(_overview.AssetsPerType, _overview.AssetsPerAccount);
    private NameValueListCardModel? ExpenseModel => _overview is null ? null : new(_overview.ExpenseDistribution);

    protected override async Task OnInitializedAsync()
    {
        // A rolling last-31-days window rather than the current calendar month, so charts always have
        // ~a month of history — the current-month range is nearly empty on the 1st. #685
        // Shared with the Assets and Liabilities pages so the three cannot drift apart. #700
        var (Start, End) = DateRangeHelper.GetDefaultOverviewRange(DateTime.UtcNow);
        StartDate = Start;
        EndDate = End;

        await CardVisibility.EnsureLoadedAsync();
        await LoadOverview();
    }

    // A card renders when the user has not hidden it and it is not auto-hidden as empty.
    // Empty auto-hiding only applies once an overview is loaded; while self-loading the
    // dashboard has no data to judge emptiness, so those cards keep rendering.
    private bool IsCardVisible(string cardId) =>
        !CardVisibility.IsHidden(cardId) && !IsAutoHiddenWhenEmpty(cardId);

    private bool IsAutoHiddenWhenEmpty(string cardId) =>
        _overview is not null && DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(cardId, _overview);

    // Lays out a tier's visible cards for both grid breakpoints. Below the large breakpoint
    // the grid has room for two cards per row, so third-width cards start at half width there.
    // When hidden cards would leave a gap, a card that may widen is moved into the short row;
    // the order is chosen for the large layout and the medium layout follows it, since the
    // markup has a single card order.
    private List<TierSlot> ArrangeTier(IEnumerable<TierCard> tier)
    {
        var shown = tier.Where(card => IsCardVisible(card.Id)).ToList();
        var order = DashboardCardLayout.FillingOrder([.. shown.Select(card => card.LargeSpan)]);
        var visible = order.Select(i => shown[i]).ToList();
        var large = DashboardCardLayout.Arrange([.. visible.Select(card => card.LargeSpan)]);
        var medium = DashboardCardLayout.Arrange([.. visible.Select(card =>
            new DashboardCardSpan(Math.Max(card.LargeSpan.Preferred, _half), Math.Max(card.LargeSpan.Max, _half)))]);

        return [.. visible.Select((card, i) => new TierSlot(card.Id, medium[i], large[i]))];
    }

    private async Task OpenCustomizeMenu()
    {
        if (_customizeMenu is not null)
            await _customizeMenu.OpenMenuAsync(EventArgs.Empty);
    }

    private bool AnyCardVisible => DashboardCards.All.Any(card => IsCardVisible(card.Id));

    private async Task ToggleCard(string cardId)
    {
        await CardVisibility.SetHiddenAsync(cardId, !CardVisibility.IsHidden(cardId));
        StateHasChanged();
    }

    // DashboardDatePicker exposes a synchronous Action callback, so kick off the
    // reload without awaiting; LoadOverview owns its own state and error handling.
    public void DateChanged((DateTime Start, DateTime End) changed)
    {
        StartDate = changed.Start;
        EndDate = changed.End;
        _ = LoadOverview();
    }

    // Stale-while-revalidate: paint the stored snapshot, always re-fetch, and only repaint and
    // re-persist when the fresh overview differs. SnapshotRefreshCoordinator owns that workflow —
    // see docs/architecture/concepts/ui-snapshots.md.
    private async Task LoadOverview()
    {
        // Claimed here rather than inside the coordinator so the same version also guards the
        // logged-out branch below, which commits state before the run starts.
        var requestVersion = _overviewGate.Claim();
        var startDate = StartDate;
        var endDate = EndDate;

        _hasError = false;

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            if (_overviewGate.IsCurrent(requestVersion))
            {
                _overview = null;
                _isLoading = false;
                StateHasChanged();
            }
            return;
        }

        var currency = await SettingsService.GetCurrencyAsync();

        var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<DashboardOverviewSnapshot, DashboardOverviewDto>
        {
            Key = BuildSnapshotKey(user.UserId, currency.Id),
            Gate = _overviewGate,
            ClaimedVersion = requestVersion,
            ToModel = snapshot => snapshot.ToDto(),
            FetchAsync = () => DashboardHttpClient.GetOverview(user.UserId, currency.Id, startDate, endDate),
            ToSnapshot = DashboardOverviewSnapshot.FromDto,

            // The snapshot renders the range it was captured for; fresh data renders the requested one.
            OnSnapshotPainted = overview => ShowOverview(overview, overview.StartDate, overview.EndDate),
            OnSnapshotMissing = () =>
            {
                _isLoading = true;
                StateHasChanged();
                return Task.CompletedTask;
            },
            OnRefreshed = overview => ShowOverview(overview, startDate, endDate),
        });

        if (!_overviewGate.IsCurrent(requestVersion))
            return;

        // A failed fetch behind a painted snapshot keeps the snapshot on screen; only a surface
        // with nothing to show falls back to the error state.
        if (result.IsBlockingFailure)
        {
            _overview = null;
            _hasError = true;
        }

        _isLoading = false;
        StateHasChanged();
    }

    private Task ShowOverview(DashboardOverviewDto overview, DateTime start, DateTime end)
    {
        _overview = overview;
        _overviewStart = start;
        _overviewEnd = end;
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // Per-user key with no date component, so a single snapshot per user is overwritten each save.
    // The key is currency-scoped so a snapshot saved before a preferred-currency change is never
    // painted with the new currency's labels.
    private static string BuildSnapshotKey(int userId, int currencyId) => $"dashboard-overview:{userId}:{currencyId}";

    private sealed record TierCard(string Id, DashboardCardSpan LargeSpan);

    private sealed record TierSlot(string CardId, DashboardCardPlacement Medium, DashboardCardPlacement Large);
}