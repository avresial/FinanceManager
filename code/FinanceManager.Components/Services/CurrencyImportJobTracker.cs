using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public sealed class CurrencyImportJobTracker(
    CurrencyAccountImportHttpClient importClient,
    NavigationManager navigation,
    ILoginService loginService,
    ILogger<CurrencyImportJobTracker> logger) : IAsyncDisposable
{
    private HubConnection? _hub;
    private CancellationTokenSource? _polling;

    public event Action? Changed;
    public Guid? JobId { get; private set; }
    public CurrencyImportJobStatusDto? Status { get; private set; }
    public IReadOnlyList<ImportJobConflict> Conflicts { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task StartAsync(Guid jobId)
    {
        await StopAsync();
        JobId = jobId;
        Status = null;
        Conflicts = [];
        Error = null;
        await EnsureHubAsync();
        await _hub!.InvokeAsync("JoinJob", jobId.ToString());
        await RefreshAsync();
        _polling = new CancellationTokenSource();
        _ = PollAsync(_polling.Token);
    }

    public async Task ResetAsync()
    {
        await StopAsync();
        JobId = null;
        Status = null;
        Conflicts = [];
        Error = null;
        Changed?.Invoke();
    }

    public void MarkResolved(IReadOnlyCollection<string> conflictIds)
    {
        if (conflictIds.Count == 0) return;
        Conflicts = Conflicts.Select(x => conflictIds.Contains(x.ConflictId) ? x with { IsResolved = true } : x).ToList();
        if (Status is not null)
        {
            var count = Conflicts.Count(x => !x.Conflict.IsExactMatch && x.IsResolved);
            Status = Status with { ResolvedConflictCount = Math.Max(Status.ResolvedConflictCount, count) };
        }
        Changed?.Invoke();
    }

    internal bool ApplyStatus(CurrencyImportJobStatusDto status)
    {
        if (JobId != status.JobId || Status is not null && status.ProcessedEntries < Status.ProcessedEntries) return false;
        Status = status;
        Conflicts = MergeConflicts(Conflicts, status.Conflicts);
        if (status.IsCompleted && status.Failed > 0)
            Error = $"Import completed with {status.Failed} failed entr{(status.Failed == 1 ? "y" : "ies")}.";
        if (status.Errors.Count > 0) Error = status.Errors.LastOrDefault();
        return true;
    }

    internal static IReadOnlyList<ImportJobConflict> MergeConflicts(IReadOnlyCollection<ImportJobConflict> current, IReadOnlyCollection<ImportJobConflict> incoming)
    {
        var currentById = current.ToDictionary(x => x.ConflictId);
        return [.. incoming.Select(x => currentById.TryGetValue(x.ConflictId, out var old) && old.IsResolved ? x with { IsResolved = true } : x),
            .. current.Where(x => incoming.All(y => y.ConflictId != x.ConflictId))];
    }

    private async Task EnsureHubAsync()
    {
        _hub = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri("hubs/currency-import"), options =>
                options.AccessTokenProvider = async () => (await loginService.GetLoggedUser())?.Token)
            .WithAutomaticReconnect().Build();
        _hub.On<CurrencyImportJobStatusDto>("ImportStatusUpdated", status => { if (ApplyStatus(status)) Changed?.Invoke(); });
        _hub.On<ImportJobConflict>("ConflictDiscovered", conflict =>
        {
            if (JobId is null || Conflicts.Any(x => x.ConflictId == conflict.ConflictId)) return;
            Conflicts = [.. Conflicts, conflict];
            Changed?.Invoke();
        });
        await _hub.StartAsync();
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RefreshAsync();
                if (Status?.IsCompleted == true) break;
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RefreshAsync()
    {
        if (JobId is not Guid jobId) return;
        try
        {
            var status = await importClient.GetCurrencyImportStatusAsync(jobId);
            if (status is not null && ApplyStatus(status)) Changed?.Invoke();
        }
        catch (Exception ex) { logger.LogDebug(ex, "Unable to refresh import job status"); }
    }

    private async Task StopAsync()
    {
        _polling?.Cancel();
        _polling?.Dispose();
        _polling = null;
        if (_hub is not null) await _hub.DisposeAsync();
        _hub = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}