using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminLogsCard : ComponentBase, IAsyncDisposable
{
    private const int _maxEntries = 5;

    // Gate only orders REST reads against each other (initial load vs reconnect re-read) and dispose.
    // Live entries never claim it: entries are a set, so they are merged instead of superseding reads.
    private readonly RefreshVersionGate _gate = new();
    // Last list from a snapshot or a REST read (already merged with live entries); null until one exists.
    private List<AdminLogEntryView>? _baseline;
    // Top entries received from the hub since the card was created. Every REST result and snapshot paint
    // is merged with these, so a late read can never drop a newer live entry.
    private List<AdminLogEntryView> _live = [];
    private string? _loadError;
    private string? _liveUpdatesError;
    private HubConnection? _hubConnection;
    private int? _userId;
    private bool _disposed;

    [Inject] public required AdminLogsHttpClient AdminLogsHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ISnapshotService SnapshotService { get; set; }
    [Inject] public required ILogger<AdminLogsCard> Logger { get; set; }

    /// <summary>What the card shows: the committed list merged with live entries, or null while nothing is known.</summary>
    private List<AdminLogEntryView>? Entries =>
        _baseline is null && _live.Count == 0 ? null : Merge(_baseline ?? [], _live);

    protected override async Task OnInitializedAsync()
    {
        _userId = (await LoginService.GetLoggedUser())?.UserId;

        // The hub starts concurrently so entries arriving during the REST read are not missed; the merge keeps them.
        await Task.WhenAll(RefreshAsync(), ConnectHub());
    }

    /// <summary>Paints the stored snapshot, then reads the latest entries and merges them with live ones.</summary>
    internal async Task RefreshAsync()
    {
        var version = _gate.Claim();

        if (_userId is not int userId)
        {
            await RefreshWithoutSnapshotAsync(version);
            return;
        }

        var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<AdminLogsCardSnapshot, List<AdminLogEntryView>>
        {
            Key = SnapshotKey(userId),
            Gate = _gate,
            ClaimedVersion = version,
            ToModel = snapshot => snapshot.UserId == userId ? [.. snapshot.Entries] : null,
            FetchAsync = async () => await FetchMergedAsync(),
            ToSnapshot = entries => ToSnapshot(userId, entries),
            ContentComparer = DisplayedContentComparer.Instance,
            OnSnapshotPainted = PaintStoredAsync,
            OnRefreshed = CommitAsync,
        });

        // A failed read only blocks the card while nothing is on screen.
        if (result.IsBlockingFailure && Entries is null)
        {
            _loadError = $"Failed to load logs: {result.Error?.Message}";
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshWithoutSnapshotAsync(int version)
    {
        try
        {
            var merged = await FetchMergedAsync();
            if (_gate.IsCurrent(version))
                await CommitAsync(merged);
        }
        catch (Exception ex)
        {
            if (Entries is null)
            {
                _loadError = $"Failed to load logs: {ex.Message}";
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>Reads the latest entries and merges the live ones as of response time, so the merged list is what gets compared and stored.</summary>
    private async Task<List<AdminLogEntryView>> FetchMergedAsync()
    {
        var latest = await AdminLogsHttpClient.GetLatest(_maxEntries);
        return Merge(latest.Select(ToView), _live);
    }

    /// <summary>Merges a hub batch into the render and the stored snapshot.</summary>
    internal async Task ApplyLiveBatchAsync(LogEntryDto[] batch)
    {
        if (batch is null || batch.Length == 0)
            return;

        _live = Merge(_live, batch.Select(ToView));
        _loadError = null;
        if (!_disposed)
            await InvokeAsync(StateHasChanged);

        if (_userId is not int userId || Entries is not List<AdminLogEntryView> entries)
            return;

        try
        {
            await SnapshotService.SetAsync(SnapshotKey(userId), ToSnapshot(userId, entries));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Live logs rendered but saving the snapshot failed.");
        }
    }

    private static string SnapshotKey(int userId) => $"admin-logs:{userId}";

    private static AdminLogsCardSnapshot ToSnapshot(int userId, List<AdminLogEntryView> entries) =>
        new() { UserId = userId, Entries = entries };

    private static AdminLogEntryView ToView(LogEntryDto entry) =>
        new(entry.Id, entry.TimestampUtc, entry.Level, entry.Category, entry.Message);

    /// <summary>Union by Id, newest first (timestamp, then Id), capped to the card size. Shared by the live and REST paths.</summary>
    private static List<AdminLogEntryView> Merge(IEnumerable<AdminLogEntryView> first, IEnumerable<AdminLogEntryView> second) =>
        first.Concat(second)
            .DistinctBy(e => e.Id)
            .OrderByDescending(e => e.TimestampUtc)
            .ThenByDescending(e => e.Id)
            .Take(_maxEntries)
            .ToList();

    private async Task PaintStoredAsync(List<AdminLogEntryView> stored)
    {
        // Never replace a list already committed (e.g. by a reconnect re-read); live entries are merged in at render time.
        if (_baseline is null)
        {
            _baseline = stored;
            if (!_disposed)
                await InvokeAsync(StateHasChanged);
        }
    }

    private async Task CommitAsync(List<AdminLogEntryView> merged)
    {
        _baseline = merged;
        _loadError = null;
        if (!_disposed)
            await InvokeAsync(StateHasChanged);
    }

    private async Task ConnectHub()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(NavigationManager.ToAbsoluteUri("hubs/admin-logs"), options =>
                {
                    options.AccessTokenProvider = async () => (await LoginService.GetLoggedUser())?.Token;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<LogEntryDto[]>("LogsAppended", ApplyLiveBatchAsync);

            _hubConnection.Reconnected += async _ =>
            {
                if (_hubConnection is not null)
                    await _hubConnection.InvokeAsync("Subscribe");
                // Entries missed while disconnected are recovered by a read that is merged with live ones.
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

    private static Color LevelColor(LogSeverity level) => level switch
    {
        LogSeverity.Critical => Color.Error,
        LogSeverity.Error => Color.Error,
        LogSeverity.Warning => Color.Warning,
        _ => Color.Default,
    };

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

    /// <summary>Compares only the displayed fields in order, so an unchanged list never repaints or rewrites.</summary>
    private sealed class DisplayedContentComparer : IEqualityComparer<List<AdminLogEntryView>>
    {
        public static DisplayedContentComparer Instance { get; } = new();

        public bool Equals(List<AdminLogEntryView>? x, List<AdminLogEntryView>? y) =>
            x is null || y is null ? x is null && y is null : x.SequenceEqual(y);

        public int GetHashCode(List<AdminLogEntryView> obj) =>
            obj.Aggregate(0, (hash, entry) => HashCode.Combine(hash, entry));
    }
}