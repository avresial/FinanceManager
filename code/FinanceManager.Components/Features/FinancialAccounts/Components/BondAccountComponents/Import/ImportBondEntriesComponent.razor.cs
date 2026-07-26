using FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Import;

public partial class ImportBondEntriesComponent : ImportEntriesComponentBase<BondAccount, ImportBondEntriesComponent>
{
    private List<string> _summaryInfos = [];
    private string? _selectedPostingDateHeader;
    private string? _selectedValueChangeHeader;
    private string? _selectedBondHeader;
    private List<(DateTime PostingDate, decimal ValueChange, int BondDetailsId, string BondName)> _mappedPreview = [];
    private BondImportResult? _importResult;
    private bool _conflictsResolved;
    private List<BondDetails> _possibleBonds = [];

    [Inject] public required BondAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }

    private BondEntryConflictResolver? _resolverRef;

    protected override int MinimumHeaderCount => 3;
    protected override bool HasMappedPreview => _mappedPreview.Any();
    protected override bool CanSubmitConflicts => _resolverRef?.AllResolved == true;
    protected override bool HasUnresolvedConflicts =>
        !_conflictsResolved &&
        _importResult is not null &&
        _importResult.Conflicts.Any(c => !c.IsExactMatch);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _possibleBonds = await BondDetailsHttpClient.GetAll();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load bond details for import.");
        }

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
                case "Bond":
                    _selectedBondHeader = suggestion.OriginalHeaderName;
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
            string.IsNullOrWhiteSpace(_selectedBondHeader))
        {
            _step2Complete = false;
            return;
        }

        try
        {
            _mappedPreview = BondImportMapper
                .MapEntries(_selectedPostingDateHeader, _selectedValueChangeHeader, _selectedBondHeader, _headers, _rawPreview, _possibleBonds)
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

            var entries = BondImportMapper
                .MapEntries(_selectedPostingDateHeader!, _selectedValueChangeHeader!, _selectedBondHeader!, headers, data, _possibleBonds)
                .Select(x => new BondEntryImportRecordDto(x.PostingDate, x.ValueChange, x.BondDetailsId))
                .ToList();

            _importResult = await AccountImportHttpClient.ImportBondEntriesAsync(new(AccountId, entries));
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
        _selectedBondHeader = null;
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

        if (string.IsNullOrEmpty(_selectedBondHeader))
            throw new Exception("Bond column is not selected.");
    }

    private async Task SaveMappingChoices()
    {
        if (string.IsNullOrEmpty(_selectedPostingDateHeader) ||
            string.IsNullOrEmpty(_selectedValueChangeHeader) ||
            string.IsNullOrEmpty(_selectedBondHeader))
            return;

        HeaderMappingRequestItemDto[] mappingItems =
        [
            new(_selectedPostingDateHeader, "PostingDate"),
            new(_selectedValueChangeHeader, "ValueChange"),
            new(_selectedBondHeader, "Bond")
        ];

        await SaveMappingChoices(
            mappingItems,
            "Bond mapping choices saved successfully",
            "Failed to save bond mapping choices");
    }

    private void AddImportMessages(BondImportResult? importResult)
    {
        if (importResult is null)
            return;

        if (importResult.Imported != 0)
            _summaryInfos.Add($"Imported {importResult.Imported} entries.");

        AddConflictWarnings(importResult.Conflicts);
    }

    private void AddConflictWarnings(IReadOnlyCollection<BondImportConflict> conflicts)
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