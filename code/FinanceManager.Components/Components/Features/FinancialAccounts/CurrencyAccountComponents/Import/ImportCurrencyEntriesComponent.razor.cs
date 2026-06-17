using FinanceManager.Components.Components.Features.FinancialAccounts.Shared.Import;
using FinanceManager.Components.DtoMapping;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.CurrencyAccountComponents.Import;

public partial class ImportCurrencyEntriesComponent :
    ImportEntriesComponentBase<CurrencyAccount, ImportCurrencyEntriesComponent>,
    IAsyncDisposable
{
    private List<ImportJobConflict> _liveConflicts = [];
    private string? _selectedPostingDateHeader;
    private string? _selectedValueChangeHeader;
    private string? _selectedContractorDetailsHeader;
    private string? _selectedDescriptionHeader;
    private List<(DateTime PostingDate, decimal ValueChange, string? ContractorDetails, string? Description)> _mappedPreview = [];
    private ImportResult? _importResult;
    private Guid? _activeImportJobId;
    private CurrencyImportJobStatusDto? _activeJobStatus;
    private string? _jobError;
    private HubConnection? _hubConnection;
    private CancellationTokenSource? _jobPollingCts;

    [Inject] public required CurrencyAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required CurrencyAccountHttpClient AccountHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private CurrencyEntryConflictResolver? _resolverRef;

    protected override int MinimumHeaderCount => 2;
    protected override bool HasMappedPreview => _mappedPreview.Any();
    protected override bool CanSubmitConflicts => _resolverRef?.AllResolved == true;
    protected override bool HasUnresolvedConflicts =>
        (_activeImportJobId.HasValue && _liveConflicts.Any(x => !x.IsResolved && !x.Conflict.IsExactMatch)) ||
        (_importResult is not null && _importResult.Conflicts.Any());

    private int UnresolvedConflictCount => Math.Max(0, (_activeJobStatus?.ConflictCount ?? 0) - (_activeJobStatus?.ResolvedConflictCount ?? 0));
    private double ImportProgressValue => _activeJobStatus?.TotalEntries > 0
        ? (double)_activeJobStatus.ProcessedEntries / _activeJobStatus.TotalEntries * 100d
        : 0d;
    private bool ShowImportProgress => _activeJobStatus?.Status is AsyncImportJobState.Queued or AsyncImportJobState.Running;
    private bool CanReturnToAccount => _activeImportJobId.HasValue &&
        (_activeJobStatus?.IsCompleted ?? false) &&
        UnresolvedConflictCount == 0 &&
        _liveConflicts.All(x => x.IsResolved || x.Conflict.IsExactMatch);
    private bool ShowAsyncImportCompletedMessage => _stepIndex == 2 && CanReturnToAccount;
    private bool ShowSynchronousImportCompletedMessage => _stepIndex == 2 &&
        !_activeImportJobId.HasValue &&
        (_activeJobStatus?.IsCompleted ?? true) &&
        _step3Complete &&
        _importResult is not null &&
        !_importResult.Conflicts.Any();

    protected override bool ShowImportCompletedMessage => ShowAsyncImportCompletedMessage || ShowSynchronousImportCompletedMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadAccountName();
    }

    protected override Task<(List<string> Headers, List<List<string>> Data)?> ReadCsvAsync(
        string content,
        string delimiter,
        CancellationToken cancellationToken)
    {
        return ImportCurrencyModelReader.Read(content, delimiter, cancellationToken);
    }

    protected override Task ApplySuggestedMappings()
    {
        return ApplySuggestedMappings(suggestion =>
        {
            switch (suggestion.MappedFieldName)
            {
                case "PostingDate":
                    _selectedPostingDateHeader = suggestion.OriginalHeaderName;
                    break;
                case "ValueChange":
                    _selectedValueChangeHeader = suggestion.OriginalHeaderName;
                    break;
                case "ContractorDetails":
                    _selectedContractorDetailsHeader = suggestion.OriginalHeaderName;
                    break;
                case "Description":
                    _selectedDescriptionHeader = suggestion.OriginalHeaderName;
                    break;
            }
        });
    }

    protected override void OnMappingChanged()
    {
        _erorrs.Clear();
        _mappedPreview.Clear();

        if (string.IsNullOrWhiteSpace(_selectedPostingDateHeader) ||
            string.IsNullOrWhiteSpace(_selectedValueChangeHeader))
        {
            _step2Complete = false;
            return;
        }

        try
        {
            _mappedPreview = CurrencyImportMapper
                .MapEntries(
                    _selectedPostingDateHeader,
                    _selectedValueChangeHeader,
                    _selectedContractorDetailsHeader,
                    _selectedDescriptionHeader,
                    _headers,
                    _rawPreview)
                .ToList();
        }
        catch (Exception ex)
        {
            _erorrs.Add(ex.Message);
        }

        _step2Complete = _erorrs.Count == 0 && _mappedPreview.Count != 0;
    }

    protected override async Task BeginImport()
    {
        _isImportingData = true;
        _warnings.Clear();
        _jobError = null;
        _liveConflicts.Clear();
        _activeImportJobId = null;
        _activeJobStatus = null;

        if (string.IsNullOrEmpty(_uploadedContent))
        {
            _erorrs.Add("No data to import.");
            _step3Complete = false;
            _isImportingData = false;
            return;
        }

        try
        {
            EnsureRequiredMapping();

            var (headers, data) = await ReadCsvAsync(_uploadedContent, Delimiter, CancellationToken.None)
                ?? throw new Exception("Failed to read data for import.");

            var entries = CurrencyImportMapper
                .MapEntries(
                    _selectedPostingDateHeader!,
                    _selectedValueChangeHeader!,
                    _selectedContractorDetailsHeader,
                    _selectedDescriptionHeader,
                    headers,
                    data)
                .Select(x => new CurrencyEntryImportRecordDto(x.PostingDate, x.ValueChange, x.ContractorDetails, x.Description))
                .ToList();

            var startResponse = await AccountImportHttpClient.StartAsyncCurrencyImportAsync(new(AccountId, entries));
            if (startResponse is null)
                throw new Exception("Async import could not be started.");

            _activeImportJobId = startResponse.JobId;

            await EnsureHubConnection();
            await JoinJobRoom();
            await RefreshJobStatus();
            StartStatusPolling();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Import start failed");
            _erorrs.Add($"Import start failed - {ex.Message}");
            _step3Complete = false;
            _isImportingData = false;
            return;
        }

        await SaveMappingChoices();

        _step3Complete = true;
        _isImportingData = false;
    }

    public override async Task Clear()
    {
        await StopImportTracking();
        await base.Clear();

        _liveConflicts.Clear();
        _activeImportJobId = null;
        _activeJobStatus = null;
        _jobError = null;
        _importResult = null;
    }

    protected override void ResetMappingState()
    {
        _selectedPostingDateHeader = null;
        _selectedValueChangeHeader = null;
        _selectedContractorDetailsHeader = null;
        _selectedDescriptionHeader = null;
        _mappedPreview.Clear();
    }

    protected override async Task SubmitConflictsAsync()
    {
        if (_resolverRef is not null)
            await _resolverRef.SubmitAsync();
    }

    private void EnsureRequiredMapping()
    {
        if (string.IsNullOrEmpty(_selectedPostingDateHeader))
            throw new Exception("Posting date header is not selected.");

        if (string.IsNullOrEmpty(_selectedValueChangeHeader))
            throw new Exception("Value change header is not selected.");
    }

    private async Task SaveMappingChoices()
    {
        if (string.IsNullOrEmpty(_selectedPostingDateHeader) ||
            string.IsNullOrEmpty(_selectedValueChangeHeader))
            return;

        var mappingItems = new List<HeaderMappingRequestItemDto>
        {
            new(_selectedPostingDateHeader, "PostingDate"),
            new(_selectedValueChangeHeader, "ValueChange")
        };

        if (!string.IsNullOrEmpty(_selectedContractorDetailsHeader))
            mappingItems.Add(new(_selectedContractorDetailsHeader, "ContractorDetails"));

        if (!string.IsNullOrEmpty(_selectedDescriptionHeader))
            mappingItems.Add(new(_selectedDescriptionHeader, "Description"));

        await SaveMappingChoices(
            mappingItems,
            "Mapping choices saved successfully",
            "Failed to save mapping choices");
    }

    private async Task EnsureHubConnection()
    {
        if (_hubConnection is not null)
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
                await _hubConnection.StartAsync();

            return;
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("hubs/currency-import"), options =>
            {
                options.AccessTokenProvider = async () => (await LoginService.GetLoggedUser())?.Token;
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<CurrencyImportJobStatusDto>("ImportStatusUpdated", status =>
        {
            if (ApplyJobStatus(status))
                _ = InvokeAsync(StateHasChanged);
        });

        _hubConnection.On<ImportJobConflict>("ConflictDiscovered", conflict =>
        {
            if (_activeImportJobId is null)
                return;

            if (_liveConflicts.Any(x => x.ConflictId == conflict.ConflictId))
                return;

            _liveConflicts.Add(conflict);
            _ = InvokeAsync(StateHasChanged);
        });

        await _hubConnection.StartAsync();
    }

    private async Task JoinJobRoom()
    {
        if (_hubConnection is null || _activeImportJobId is null)
            return;

        await _hubConnection.InvokeAsync("JoinJob", _activeImportJobId.Value.ToString());
    }

    private void StartStatusPolling()
    {
        if (_activeImportJobId is null)
            return;

        _jobPollingCts?.Cancel();
        _jobPollingCts?.Dispose();
        _jobPollingCts = new CancellationTokenSource();

        _ = PollStatus(_jobPollingCts.Token);
    }

    private async Task PollStatus(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (!cancellationToken.IsCancellationRequested)
        {
            var hasNext = await timer.WaitForNextTickAsync(cancellationToken);
            if (!hasNext)
                break;

            await RefreshJobStatus();

            if (_activeJobStatus?.IsCompleted == true)
                break;
        }
    }

    private async Task RefreshJobStatus()
    {
        if (_activeImportJobId is null)
            return;

        try
        {
            var status = await AccountImportHttpClient.GetCurrencyImportStatusAsync(_activeImportJobId.Value);
            if (status is null)
                return;

            if (!ApplyJobStatus(status))
                return;

            if (status.IsCompleted && status.Failed > 0)
                _jobError = $"Import completed with {status.Failed} failed entr{(status.Failed == 1 ? "y" : "ies")}.";

            if (status.Errors.Count > 0)
                _jobError = status.Errors.LastOrDefault();

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Unable to refresh import job status");
        }
    }

    private Task OnConflictsResolved(IReadOnlyCollection<string> conflictIds)
    {
        if (conflictIds.Count == 0)
            return Task.CompletedTask;

        _liveConflicts = _liveConflicts
            .Select(x => conflictIds.Contains(x.ConflictId) ? x with { IsResolved = true } : x)
            .ToList();

        if (_activeJobStatus is not null)
        {
            var resolvedCount = _liveConflicts.Count(x => !x.Conflict.IsExactMatch && x.IsResolved);
            _activeJobStatus = _activeJobStatus with
            {
                ResolvedConflictCount = Math.Max(_activeJobStatus.ResolvedConflictCount, resolvedCount)
            };
        }

        return Task.CompletedTask;
    }

    private bool ApplyJobStatus(CurrencyImportJobStatusDto status)
    {
        if (_activeImportJobId != status.JobId)
            return false;

        if (_activeJobStatus is not null && status.ProcessedEntries < _activeJobStatus.ProcessedEntries)
            return false;

        _activeJobStatus = status;
        _liveConflicts = MergeConflicts(_liveConflicts, status.Conflicts);
        return true;
    }

    private static List<ImportJobConflict> MergeConflicts(
        IReadOnlyCollection<ImportJobConflict> current,
        IReadOnlyCollection<ImportJobConflict> incoming)
    {
        var currentById = current.ToDictionary(x => x.ConflictId);
        var merged = new List<ImportJobConflict>();

        foreach (var incomingConflict in incoming)
        {
            if (currentById.TryGetValue(incomingConflict.ConflictId, out var currentConflict) && currentConflict.IsResolved)
                merged.Add(incomingConflict with { IsResolved = true });
            else
                merged.Add(incomingConflict);
        }

        foreach (var currentConflict in current)
        {
            if (!incoming.Any(x => x.ConflictId == currentConflict.ConflictId))
                merged.Add(currentConflict);
        }

        return merged;
    }

    private async Task StopImportTracking()
    {
        if (_jobPollingCts is not null)
        {
            _jobPollingCts.Cancel();
            _jobPollingCts.Dispose();
            _jobPollingCts = null;
        }

        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopImportTracking();
            DisposePreviewRegeneration();
        }
        catch
        {
            // Best-effort cleanup for component disposal.
        }
    }
}