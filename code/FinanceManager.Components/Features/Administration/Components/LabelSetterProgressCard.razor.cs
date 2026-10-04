using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class LabelSetterProgressCard : ComponentBase, IAsyncDisposable
{
    // One gate for every writer (REST load, reconnect read, live event, dispose): whoever claims last owns the state.
    private readonly RefreshVersionGate _gate = new();
    private LabelSetterProgressSnapshot? _snapshot;
    private string? _loadError;
    private string? _liveUpdatesError;
    private HubConnection? _hubConnection;
    private int? _userId;
    private bool _disposed;

    [Inject] public required LabelSetterProgressHttpClient ProgressHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ISnapshotService SnapshotService { get; set; }
    [Inject] public required ILogger<LabelSetterProgressCard> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _userId = (await LoginService.GetLoggedUser())?.UserId;

        // The hub starts concurrently so events arriving during the REST read are not missed; the gate keeps the order right.
        await Task.WhenAll(RefreshAsync(), ConnectHub());
    }

    /// <summary>Paints the stored snapshot, then reads the current server state through the shared gate.</summary>
    internal async Task RefreshAsync()
    {
        var version = _gate.Claim();

        if (_userId is not int userId)
        {
            await RefreshWithoutSnapshotAsync(version);
            return;
        }

        LabelSetterProgressSnapshot? fetched = null;
        var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<LabelSetterProgressCardSnapshot, LabelSetterProgressSnapshot>
        {
            Key = $"admin-label-setter-progress:{userId}",
            Gate = _gate,
            ClaimedVersion = version,
            ToModel = snapshot => snapshot.UserId == userId ? snapshot.Progress : null,
            FetchAsync = async () => fetched = await ProgressHttpClient.GetSnapshot(),
            ToSnapshot = progress => ToSnapshot(userId, progress),
            ContentComparer = DisplayedContentComparer.Instance,
            OnSnapshotPainted = PaintStoredAsync,
            OnRefreshed = ShowAsync,
        });

        // The coordinator compares against storage, which lags the screen when a live event's write failed,
        // so a current successful read is applied to whatever is displayed even when it reports Unchanged.
        if (fetched is not null && result.Outcome != SnapshotRefreshOutcome.Failed && _gate.IsCurrent(version))
        {
            _loadError = null;
            await ShowAsync(fetched);
            return;
        }

        // A failed read only blocks the card while nothing is on screen (a live event may already have filled it).
        if (result.IsBlockingFailure && _snapshot is null)
        {
            _loadError = $"Failed to load progress: {result.Error?.Message}";
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshWithoutSnapshotAsync(int version)
    {
        try
        {
            var progress = await ProgressHttpClient.GetSnapshot();
            if (progress is not null && _gate.IsCurrent(version))
            {
                _loadError = null;
                await ShowAsync(progress);
            }
        }
        catch (Exception ex)
        {
            if (_snapshot is null)
            {
                _loadError = $"Failed to load progress: {ex.Message}";
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>Applies a progress event from the hub; it supersedes any read still in flight.</summary>
    internal async Task ApplyLiveProgressAsync(LabelSetterProgressSnapshot progress)
    {
        _gate.Claim();
        _loadError = null;
        _liveUpdatesError = null;
        await ShowAsync(progress);

        if (_userId is not int userId)
            return;

        try
        {
            await SnapshotService.SetAsync($"admin-label-setter-progress:{userId}", ToSnapshot(userId, progress));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Live label setter progress rendered but saving the snapshot failed.");
        }
    }

    private static LabelSetterProgressCardSnapshot ToSnapshot(int userId, LabelSetterProgressSnapshot progress) =>
        new() { UserId = userId, Progress = progress };

    private async Task PaintStoredAsync(LabelSetterProgressSnapshot progress)
    {
        // Never replace a state that is already on screen with an older stored one.
        if (_snapshot is null)
            await ShowAsync(progress);
    }

    private async Task ShowAsync(LabelSetterProgressSnapshot progress)
    {
        _snapshot = progress;
        if (!_disposed)
            await InvokeAsync(StateHasChanged);
    }

    private async Task ConnectHub()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(NavigationManager.ToAbsoluteUri("hubs/label-setter-progress"), options =>
                {
                    options.AccessTokenProvider = async () => (await LoginService.GetLoggedUser())?.Token;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<LabelSetterProgressSnapshot>("ProgressUpdated", ApplyLiveProgressAsync);

            _hubConnection.Reconnected += async _ =>
            {
                await _hubConnection.InvokeAsync("Subscribe");
                _liveUpdatesError = null;
                if (!_disposed)
                    await InvokeAsync(StateHasChanged);
                // Events missed while disconnected are recovered by a read that a newer event can still outrank.
                await RefreshAsync();
            };

            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("Subscribe");
        }
        catch (Exception ex)
        {
            _liveUpdatesError = $"Live updates disabled: {ex.Message}";
            if (!_disposed)
                await InvokeAsync(StateHasChanged);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _gate.Claim();
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    /// <summary>Compares only what the card displays, so an unrelated field change never repaints or rewrites.</summary>
    private sealed class DisplayedContentComparer : IEqualityComparer<LabelSetterProgressSnapshot>
    {
        public static DisplayedContentComparer Instance { get; } = new();

        public bool Equals(LabelSetterProgressSnapshot? x, LabelSetterProgressSnapshot? y)
        {
            if (x is null || y is null)
                return x is null && y is null;
            if (x.QueuedJobsCount != y.QueuedJobsCount)
                return false;
            if (x.CurrentJob is null || y.CurrentJob is null)
                return x.CurrentJob is null && y.CurrentJob is null;
            return x.CurrentJob.UserId == y.CurrentJob.UserId
                && x.CurrentJob.AccountId == y.CurrentJob.AccountId
                && x.CurrentJob.ProcessedEntries == y.CurrentJob.ProcessedEntries
                && x.CurrentJob.TotalEntries == y.CurrentJob.TotalEntries;
        }

        public int GetHashCode(LabelSetterProgressSnapshot obj) => HashCode.Combine(
            obj.QueuedJobsCount,
            obj.CurrentJob?.UserId,
            obj.CurrentJob?.AccountId,
            obj.CurrentJob?.ProcessedEntries,
            obj.CurrentJob?.TotalEntries);
    }
}