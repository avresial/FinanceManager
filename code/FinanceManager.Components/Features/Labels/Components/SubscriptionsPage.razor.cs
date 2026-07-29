using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Labels.Models;
using FinanceManager.Components.Features.Labels.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Labels.Components;

/// <summary>
/// Coordinator for the Subscriptions page: resolves user and currency, runs the stale-while-revalidate
/// snapshot workflow for each of the page's two surfaces, and hands finished models to the views.
/// </summary>
/// <remarks>
/// The summary tiles and the list are snapshotted separately — separate keys, separate content
/// equality — so a change that only moves the list (a renamed pattern, a new charge date) does not
/// repaint or rewrite the tiles, and vice versa. Both are fed from the single subscriptions request
/// the page already made, so splitting the snapshots costs no extra HTTP call. See
/// docs/codebase/UI-SNAPSHOTS.md.
/// </remarks>
public partial class SubscriptionsPage : ComponentBase
{
    private bool _isSummaryLoading = true;
    private bool _isListLoading = true;
    private bool _showInactive;
    private int? _userId;
    private Currency _currency = DefaultCurrency.PLN;
    private SubscriptionsSummaryModel _summary = SubscriptionsSummaryModel.Empty;
    private List<RecurringTransactionResult> _subscriptions = [];
    private readonly HashSet<Guid> _updating = [];

    // Shared by both surfaces so one reload claims a single version for the pair; a newer reload
    // then supersedes both together rather than leaving the tiles and the list from different runs.
    private readonly RefreshVersionGate _refreshGate = new();

    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required RecurringTransactionDetectorHttpClient HttpClient { get; set; }
    [Inject] public required SubscriptionsSnapshotStore SnapshotStore { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<SubscriptionsPage> Logger { get; set; }

    protected override Task OnInitializedAsync() => LoadData();

    private async Task LoadData()
    {
        // Claimed here rather than inside the coordinator so the same version guards the logged-out
        // branch below and is shared by both surfaces.
        var version = _refreshGate.Claim();

        var user = await LoginService.GetLoggedUser();
        if (!_refreshGate.IsCurrent(version)) return;
        if (user is null)
        {
            _isSummaryLoading = false;
            _isListLoading = false;
            StateHasChanged();
            return;
        }

        _userId = user.UserId;
        _currency = await SettingsService.GetCurrencyAsync();
        if (!_refreshGate.IsCurrent(version)) return;

        var currency = _currency.ShortName;

        // One request feeds both surfaces: the first surface to reach its fetch starts it, the other
        // awaits the same task. Lazy is thread-safe by default, so neither ordering issues a second call.
        var subscriptions = new Lazy<Task<List<RecurringTransactionResult>>>(
            () => HttpClient.GetRecurringTransactions(user.UserId));

        // Run both surfaces concurrently so each paints its own snapshot as soon as it is read,
        // instead of the list waiting for the tiles to finish reconciling.
        var summaryRun = SnapshotStore.RefreshSummaryAsync(
            user.UserId,
            currency,
            _refreshGate,
            version,
            async () => SubscriptionsSummaryModel.FromSubscriptions(await subscriptions.Value),
            onSnapshotPainted: ShowSummary,
            onSnapshotMissing: () => ShowLoading(summary: true),
            onRefreshed: ShowSummary);

        var listRun = SnapshotStore.RefreshListAsync(
            user.UserId,
            currency,
            _refreshGate,
            version,
            async () => await subscriptions.Value,
            onSnapshotPainted: ShowSubscriptions,
            onSnapshotMissing: () => ShowLoading(summary: false),
            onRefreshed: ShowSubscriptions);

        await Task.WhenAll(summaryRun, listRun);
        if (!_refreshGate.IsCurrent(version)) return;

        var summaryResult = await summaryRun;
        var listResult = await listRun;

        _isSummaryLoading = false;
        _isListLoading = false;

        // A failed request behind a painted snapshot keeps that snapshot on screen; only a surface
        // with nothing to show reports the failure. Both surfaces share the one request, so a single
        // message covers them.
        if (summaryResult.IsBlockingFailure || listResult.IsBlockingFailure)
        {
            Logger.LogError(summaryResult.Error ?? listResult.Error, "Unable to load subscriptions");
            Snackbar.Add("Unable to load subscriptions.", Severity.Error);
        }

        StateHasChanged();
    }

    private Task ShowSummary(SubscriptionsSummaryModel model)
    {
        _summary = model;
        _isSummaryLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task ShowSubscriptions(List<RecurringTransactionResult> subscriptions)
    {
        _subscriptions = subscriptions;
        _isListLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task ShowLoading(bool summary)
    {
        if (summary)
            _isSummaryLoading = true;
        else
            _isListLoading = true;

        return InvokeAsync(StateHasChanged);
    }

    private Task ToggleMuted(RecurringTransactionResult item) =>
        Update(item, !item.IsMuted, item.IsCancelled, item.IsFlaggedForReview);

    private Task ToggleCancelled(RecurringTransactionResult item) =>
        Update(item, item.IsMuted, !item.IsCancelled, item.IsFlaggedForReview);

    private Task ToggleFlag(RecurringTransactionResult item) =>
        Update(item, item.IsMuted, item.IsCancelled, !item.IsFlaggedForReview);

    private async Task Update(
        RecurringTransactionResult item,
        bool isMuted,
        bool isCancelled,
        bool isFlaggedForReview)
    {
        if (_userId is null || !_updating.Add(item.PatternId)) return;

        try
        {
            var saved = await HttpClient.Update(
                _userId.Value,
                item.PatternId,
                new UpdateRecurringSubscription(isMuted, isCancelled, isFlaggedForReview));
            if (!saved)
            {
                Snackbar.Add("Unable to update this subscription.", Severity.Error);
                return;
            }

            item.IsMuted = isMuted;
            item.IsCancelled = isCancelled;
            item.IsFlaggedForReview = isFlaggedForReview;
            _summary = SubscriptionsSummaryModel.FromSubscriptions(_subscriptions);

            await PersistSnapshots();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to update subscription {PatternId}", item.PatternId);
            Snackbar.Add("Unable to update this subscription.", Severity.Error);
        }
        finally
        {
            _updating.Remove(item.PatternId);
        }
    }

    /// <summary>
    /// Re-persists both surfaces after a toggle so the next visit paints the state the user just
    /// produced instead of the pre-toggle one. Runs through the coordinator with the current models
    /// as the "fresh" data and no paint callbacks: it reconciles storage only, without re-requesting
    /// and without repainting what is already on screen.
    /// </summary>
    private Task PersistSnapshots()
    {
        if (_userId is null) return Task.CompletedTask;

        var version = _refreshGate.Claim();
        var currency = _currency.ShortName;
        var summary = _summary;
        var subscriptions = _subscriptions;

        return Task.WhenAll(
            SnapshotStore.RefreshSummaryAsync(_userId.Value, currency, _refreshGate, version,
                () => Task.FromResult<SubscriptionsSummaryModel?>(summary)),
            SnapshotStore.RefreshListAsync(_userId.Value, currency, _refreshGate, version,
                () => Task.FromResult<List<RecurringTransactionResult>?>(subscriptions)));
    }
}