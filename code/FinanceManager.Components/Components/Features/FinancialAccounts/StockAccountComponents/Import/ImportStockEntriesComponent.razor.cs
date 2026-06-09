using FinanceManager.Components.Components.Features.FinancialAccounts.Shared.Import;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Import;

public partial class ImportStockEntriesComponent : ImportEntriesComponentBase<StockAccount, ImportStockEntriesComponent>
{
    private List<string> _summaryInfos = [];
    private string? _selectedPostingDateHeader;
    private string? _selectedValueChangeHeader;
    private string? _selectedTickerHeader;
    private List<(DateTime PostingDate, decimal ValueChange, string Ticker)> _mappedPreview = [];
    private StockImportResult? _importResult;
    private bool _conflictsResolved;

    [Inject] public required StockAccountImportHttpClient AccountImportHttpClient { get; set; }

    private StockEntryConflictResolver? _resolverRef;

    protected override int MinimumHeaderCount => 3;
    protected override bool HasMappedPreview => _mappedPreview.Any();
    protected override bool CanSubmitConflicts => _resolverRef?.AllResolved == true;
    protected override bool HasUnresolvedConflicts =>
        !_conflictsResolved &&
        _importResult is not null &&
        _importResult.Conflicts.Any(c => !c.IsExactMatch);

    protected override async Task OnInitializedAsync()
    {
        await LoadAccountName();
    }

    protected override Task<(List<string> Headers, List<List<string>> Data)?> ReadCsvAsync(
        string content,
        string delimiter,
        CancellationToken cancellationToken)
    {
        return ImportStockModelReader.Read(content, delimiter, cancellationToken);
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
                case "Ticker":
                    _selectedTickerHeader = suggestion.OriginalHeaderName;
                    break;
            }
        });
    }

    protected override void OnMappingChanged()
    {
        _erorrs.Clear();
        _mappedPreview.Clear();

        if (string.IsNullOrWhiteSpace(_selectedPostingDateHeader) ||
            string.IsNullOrWhiteSpace(_selectedValueChangeHeader) ||
            string.IsNullOrWhiteSpace(_selectedTickerHeader))
        {
            _step2Complete = false;
            return;
        }

        try
        {
            _mappedPreview = StockImportMapper
                .MapEntries(_selectedPostingDateHeader, _selectedValueChangeHeader, _selectedTickerHeader, _headers, _rawPreview)
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
        _summaryInfos.Clear();
        _warnings.Clear();
        _conflictsResolved = false;

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

            var entries = StockImportMapper
                .MapEntries(_selectedPostingDateHeader!, _selectedValueChangeHeader!, _selectedTickerHeader!, headers, data)
                .Select(x => new StockEntryImportRecordDto(x.PostingDate, x.ValueChange, x.Ticker))
                .ToList();

            _importResult = await AccountImportHttpClient.ImportStockEntriesAsync(new(AccountId, entries));
            AddImportMessages(_importResult);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Import failed");
            _erorrs.Add($"Import failed - {ex.Message}");
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
        await base.Clear();

        _summaryInfos.Clear();
        _conflictsResolved = false;
        _importResult = null;
    }

    protected override void ResetMappingState()
    {
        _selectedPostingDateHeader = null;
        _selectedValueChangeHeader = null;
        _selectedTickerHeader = null;
        _mappedPreview.Clear();
    }

    protected override async Task SubmitConflictsAsync()
    {
        if (_resolverRef is not null)
            await _resolverRef.SubmitAsync();
    }

    private void OnConflictsSubmitted()
    {
        _conflictsResolved = true;
    }

    private void EnsureRequiredMapping()
    {
        if (string.IsNullOrEmpty(_selectedPostingDateHeader))
            throw new Exception("Posting date header is not selected.");

        if (string.IsNullOrEmpty(_selectedValueChangeHeader))
            throw new Exception("Value change header is not selected.");

        if (string.IsNullOrEmpty(_selectedTickerHeader))
            throw new Exception("Ticker header is not selected.");
    }

    private async Task SaveMappingChoices()
    {
        if (string.IsNullOrEmpty(_selectedPostingDateHeader) ||
            string.IsNullOrEmpty(_selectedValueChangeHeader) ||
            string.IsNullOrEmpty(_selectedTickerHeader))
            return;

        HeaderMappingRequestItemDto[] mappingItems =
        [
            new(_selectedPostingDateHeader, "PostingDate"),
            new(_selectedValueChangeHeader, "ValueChange"),
            new(_selectedTickerHeader, "Ticker")
        ];

        await SaveMappingChoices(
            mappingItems,
            "Stock mapping choices saved successfully",
            "Failed to save stock mapping choices");
    }

    private void AddImportMessages(StockImportResult? importResult)
    {
        if (importResult is null)
            return;

        if (importResult.Imported != 0)
            _summaryInfos.Add($"Imported {importResult.Imported} entries.");

        AddConflictWarnings(importResult.Conflicts);
    }

    private void AddConflictWarnings(IReadOnlyCollection<StockImportConflict> conflicts)
    {
        if (conflicts.Count == 0)
            return;

        var exactMatches = conflicts.Count(x => x.IsExactMatch);
        var conflictDays = conflicts
            .Where(x => !x.IsExactMatch)
            .DistinctBy(x => x.DateTime.Date)
            .Count();

        if (exactMatches > 0)
            _warnings.Add($"Already uploaded rows {exactMatches}.");

        if (conflicts.Count - exactMatches > 0)
            _warnings.Add($"Conflicts to resolve {conflictDays}.");
    }
}