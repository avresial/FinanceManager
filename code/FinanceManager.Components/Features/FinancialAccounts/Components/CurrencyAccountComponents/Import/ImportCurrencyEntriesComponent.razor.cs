using FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;
using FinanceManager.Components.Features.FinancialAccounts.DtoMapping;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Import;

public partial class ImportCurrencyEntriesComponent :
    ImportEntriesComponentBase<CurrencyAccount, ImportCurrencyEntriesComponent>,
    IAsyncDisposable
{
    private string? _selectedPostingDateHeader;
    private string? _selectedValueChangeHeader;
    private string? _selectedContractorDetailsHeader;
    private string? _selectedDescriptionHeader;
    private List<(DateTime PostingDate, decimal ValueChange, string? ContractorDetails, string? Description)> _mappedPreview = [];
    private ImportResult? _importResult;
    private List<Currency> _currencies = [];
    private Currency? _currency;

    [Inject] public required CurrencyAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required CurrencyAccountHttpClient AccountHttpClient { get; set; }
    [Inject] public required CashTransactionsHttpClient CashTransactionsHttpClient { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required CurrencyImportJobTracker JobTracker { get; set; }

    private IReadOnlyList<ImportJobConflict> LiveConflicts => JobTracker.Conflicts;
    private Guid? ActiveImportJobId => JobTracker.JobId;
    private CurrencyImportJobStatusDto? ActiveJobStatus => JobTracker.Status;
    private string? JobError => JobTracker.Error;

    private CurrencyEntryConflictResolver? _resolverRef;

    protected override int MinimumHeaderCount => 2;
    protected override bool HasMappedPreview => _mappedPreview.Any();
    protected override bool HasImportTarget => AccountId is not null || _currency is not null;
    protected override bool CanSubmitConflicts => _resolverRef?.AllResolved == true;
    protected override bool HasUnresolvedConflicts =>
        (ActiveImportJobId.HasValue && LiveConflicts.Any(x => !x.IsResolved && !x.Conflict.IsExactMatch)) ||
        (_importResult is not null && _importResult.Conflicts.Any());

    private int UnresolvedConflictCount => Math.Max(0, (ActiveJobStatus?.ConflictCount ?? 0) - (ActiveJobStatus?.ResolvedConflictCount ?? 0));
    private double ImportProgressValue => ActiveJobStatus?.TotalEntries > 0
        ? (double)ActiveJobStatus.ProcessedEntries / ActiveJobStatus.TotalEntries * 100d
        : 0d;
    private bool ShowImportProgress => ActiveJobStatus?.Status is AsyncImportJobState.Queued or AsyncImportJobState.Running;
    // A failed job also reports IsCompleted, so it must not count as a successful import.
    private bool CanReturnToAccount => ActiveImportJobId.HasValue &&
        (ActiveJobStatus?.IsCompleted ?? false) &&
        ActiveJobStatus?.Status != AsyncImportJobState.Failed &&
        string.IsNullOrWhiteSpace(JobError) &&
        UnresolvedConflictCount == 0 &&
        LiveConflicts.All(x => x.IsResolved || x.Conflict.IsExactMatch);
    private bool ShowAsyncImportCompletedMessage => _stepIndex == 2 && CanReturnToAccount;
    private bool ShowImportFailedActions => _stepIndex == 2 &&
        ActiveImportJobId.HasValue &&
        (ActiveJobStatus?.IsCompleted ?? false) &&
        (ActiveJobStatus?.Status == AsyncImportJobState.Failed || !string.IsNullOrWhiteSpace(JobError));
    private bool ShowSynchronousImportCompletedMessage => _stepIndex == 2 &&
        !ActiveImportJobId.HasValue &&
        (ActiveJobStatus?.IsCompleted ?? true) &&
        _step3Complete &&
        _importResult is not null &&
        !_importResult.Conflicts.Any();

    protected override bool ShowImportCompletedMessage => ShowAsyncImportCompletedMessage || ShowSynchronousImportCompletedMessage;

    protected override async Task OnInitializedAsync()
    {
        JobTracker.Changed += OnTrackingChanged;
        await LoadAccountName();
        if (AccountId is null)
            await LoadCurrenciesAsync();
    }

    // Owner mode imports into the pool of one currency, so the wizard asks for it, starting on the user's own.
    private async Task LoadCurrenciesAsync()
    {
        try
        {
            _currencies = [.. (await CurrencyHttpClient.GetAll()).OrderBy(x => x.ShortName)];
            var preferred = SettingsService.GetCurrency();
            OnCurrencySelected(_currencies.FirstOrDefault(x => x.Id == preferred.Id) ?? _currencies.FirstOrDefault());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load currencies for the import.");
            _erorrs.Add("Could not load the currencies. Reload the page and try again.");
        }
    }

    private void OnCurrencySelected(Currency? currency)
    {
        _currency = currency;
        AccountName = currency is null ? string.Empty : $"{currency.ShortName} cash";
    }

    private void OnTrackingChanged() => _ = InvokeAsync(StateHasChanged);

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
        await JobTracker.ResetAsync();

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

            var startResponse = AccountId is int accountId
                ? await AccountImportHttpClient.StartAsyncCurrencyImportAsync(new(accountId, entries))
                : await CashTransactionsHttpClient.StartImportAsync(_currency!.Id, entries);
            if (startResponse is null)
                throw new Exception("Async import could not be started.");

            await JobTracker.StartAsync(startResponse.JobId);
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
        await JobTracker.ResetAsync();
        await base.Clear();
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

    private Task OnConflictsResolved(IReadOnlyCollection<string> conflictIds)
    {
        JobTracker.MarkResolved(conflictIds);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            JobTracker.Changed -= OnTrackingChanged;
            await JobTracker.DisposeAsync();
            DisposePreviewRegeneration();
        }
        catch
        {
            // Best-effort cleanup for component disposal.
        }
    }
}