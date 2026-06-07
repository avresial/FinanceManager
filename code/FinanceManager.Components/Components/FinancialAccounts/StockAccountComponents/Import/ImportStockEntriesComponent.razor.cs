using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Components.FinancialAccounts.Shared.Import;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents.Import;

public partial class ImportStockEntriesComponent : ComponentBase
{
    private const string _defaultDragClass = "relative rounded-lg border-2 border-dashed pa-4 mud-width-full mud-height-full";
    private string _dragClass = _defaultDragClass;

    private List<IBrowserFile> _loadedFiles = [];
    private List<string> _erorrs = [];
    private List<string> _warnings = [];
    private List<string> _summaryInfos = [];

    private List<List<string>> _rawPreview = [];
    private List<string> _headers = [];
    private string? _selectedPostingDateHeader;
    private string? _selectedValueChangeHeader;
    private string? _selectedTickerHeader;
    private List<(DateTime PostingDate, decimal ValueChange, string Ticker)> _mappedPreview = [];

    private StockImportResult? _importResult = null;
    private string? _uploadedContent;
    private string? _fileName;
    private long _fileSize;
    private int _totalRowCount;
    private bool _conflictsResolved;
    private CancellationTokenSource? _regenCts;

    private string _delimiterBacking = ",";
    private string Delimiter
    {
        get => _delimiterBacking;
        set
        {
            if (value == _delimiterBacking)
                return;
            _delimiterBacking = value;

            try
            {
                _regenCts?.Cancel();
                _regenCts?.Dispose();
            }
            catch { }
            _regenCts = new CancellationTokenSource();

            _ = RegeneratePreviewFromContentAsync(_regenCts.Token);
        }
    }

    private bool _isImportingData;
    private int _stepIndex;

    private bool _step1Complete;
    private bool _step2Complete;
    private bool _step3Complete;

    public required string AccountName { get; set; }

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<ImportStockEntriesComponent> Logger { get; set; }
    [Inject] public required StockAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required CsvHeaderMappingHttpClient MappingHttpClient { get; set; }

    private StockEntryConflictResolver? _resolverRef;
    private bool HasUnresolvedConflicts =>
        !_conflictsResolved && _importResult is not null &&
        _importResult.Conflicts.Any(c => !c.IsExactMatch);

    private string PrimaryLabel => _stepIndex switch
    {
        0 => "Continue to mapping",
        1 => "Begin import",
        2 => "Begin import",
        _ => "Continue"
    };

    private bool CanContinue => _stepIndex switch
    {
        0 => _rawPreview.Any() && _headers.Count >= 3,
        1 => !_erorrs.Any() && _mappedPreview.Any(),
        2 => _resolverRef?.AllResolved == true,
        _ => false
    };

    private bool ShowPrimaryAction => _stepIndex switch
    {
        0 or 1 => true,
        2 => HasUnresolvedConflicts,
        _ => false
    };
    private bool ShowBackAction => _stepIndex > 0 && _stepIndex < 2 && CanContinue;
    private bool ShowImportCompletedMessage => _stepIndex == 2 && _step3Complete && !HasUnresolvedConflicts;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) throw new Exception("User is null");

            var existingAccount = await FinancialAccountService.GetAccount<StockAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);
            if (existingAccount is not null)
                AccountName = existingAccount.Name;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }
    }

    private async Task RegeneratePreviewFromContentAsync(CancellationToken cancellationToken = default)
    {
        _erorrs.Clear();
        _headers.Clear();
        _rawPreview.Clear();

        if (string.IsNullOrWhiteSpace(_uploadedContent))
        {
            _step1Complete = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            var preview = await ImportCsvPreviewReader.ReadAsync(_uploadedContent, Delimiter, ImportStockModelReader.Read, cancellationToken);
            if (preview is null) return;

            _headers = preview.Headers;
            _rawPreview = preview.RawPreview;
            _totalRowCount = preview.TotalRowCount;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "CsvHelper attempt failed for delimiter {delimiter}", Delimiter);
        }

        if (_headers.Count == 0)
            _erorrs.Add("No headers found in CSV.");

        _step1Complete = _rawPreview.Count != 0;

        if (!_step1Complete && _erorrs.Count == 0)
            _erorrs.Add("Step 1 can not be completed - loading files failed.");

        if (_headers.Count > 0)
            await ApplySuggestedMappings();

        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplySuggestedMappings()
    {
        try
        {
            if (_headers.Count == 0) return;

            var suggestions = await MappingHttpClient.GetSuggestedMappingsAsync(_headers);
            if (suggestions is null || suggestions.Count == 0) return;

            foreach (var suggestion in suggestions)
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
            }

            OnMappingChanged();
        }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to get mapping suggestions");
        }
    }

    private async Task SaveMappingChoices()
    {
        try
        {
            if (string.IsNullOrEmpty(_selectedPostingDateHeader) ||
                string.IsNullOrEmpty(_selectedValueChangeHeader) ||
                string.IsNullOrEmpty(_selectedTickerHeader))
                return;

            var mappingItems = new List<HeaderMappingRequestItemDto>
            {
                new(_selectedPostingDateHeader, "PostingDate"),
                new(_selectedValueChangeHeader, "ValueChange"),
                new(_selectedTickerHeader, "Ticker"),
            };

            await MappingHttpClient.SaveMappingsAsync(new SaveMappingRequestDto(mappingItems));
            Logger?.LogInformation("Stock mapping choices saved successfully");
        }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to save stock mapping choices");
        }
    }

    private async Task UploadFiles(IBrowserFile? file)
    {
        _isImportingData = true;

        _erorrs.Clear();

        if (file is null)
        {
            _erorrs.Add("No file selected.");
            _isImportingData = false;
            return;
        }

        await Clear();

        try
        {
            var readResult = await UploadedCsvFileReader.ReadAsync(file);
            if (!readResult.Success)
            {
                _erorrs.Add(readResult.Error!);
                _isImportingData = false;
                return;
            }

            _loadedFiles = [file];
            _fileName = readResult.FileName;
            _fileSize = readResult.FileSize;
            _uploadedContent = readResult.Content;

            try
            {
                _regenCts?.Cancel();
                _regenCts?.Dispose();
            }
            catch { }

            _regenCts = new();
            await RegeneratePreviewFromContentAsync(_regenCts.Token);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to read uploaded file.");
            _erorrs.Add("Failed to read uploaded file.");
        }
        finally
        {
            _isImportingData = false;
        }
    }

    public async Task UploadFiles(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles(1))
            await UploadFiles(file);
    }

    private void OnMappingChanged()
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
            _mappedPreview = StockImportMapper.MapEntries(_selectedPostingDateHeader, _selectedValueChangeHeader, _selectedTickerHeader, _headers, _rawPreview).ToList();
        }
        catch (Exception ex)
        {
            _erorrs.Add(ex.Message);
        }

        _step2Complete = _erorrs.Count == 0 && _mappedPreview.Count != 0;
    }

    public async Task BeginImport()
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
            if (string.IsNullOrEmpty(_selectedPostingDateHeader))
                throw new Exception("Posting date header is not selected.");

            if (string.IsNullOrEmpty(_selectedValueChangeHeader))
                throw new Exception("Value change header is not selected.");

            if (string.IsNullOrEmpty(_selectedTickerHeader))
                throw new Exception("Ticker header is not selected.");

            var (Headers, Data) = await ImportStockModelReader.Read(_uploadedContent!, Delimiter, CancellationToken.None) ??
                throw new Exception("Failed to read data for import.");

            var exportResult = StockImportMapper.MapEntries(_selectedPostingDateHeader, _selectedValueChangeHeader, _selectedTickerHeader, Headers, Data);
            var entries = exportResult.Select(x => new StockEntryImportRecordDto(x.PostingDate, x.ValueChange, x.Ticker)).ToList();

            try
            {
                _importResult = await AccountImportHttpClient.ImportStockEntriesAsync(new(AccountId, entries));

                if (_importResult is not null && _importResult.Imported != 0)
                    _summaryInfos.Add($"Imported {_importResult.Imported} entries.");

                if (_importResult is not null && _importResult.Conflicts.Count != 0)
                {
                    var exactMatches = _importResult.Conflicts.Count(x => x.IsExactMatch);
                    var exactMatchesDays = _importResult.Conflicts.Where(x => !x.IsExactMatch)
                        .DistinctBy(x => x.DateTime.Date)
                        .Count();

                    if (exactMatches > 0)
                        _warnings.Add($"Already uploaded rows {exactMatches}.");

                    if (_importResult.Conflicts.Count - exactMatches > 0)
                        _warnings.Add($"Conflicts to resolve {exactMatchesDays}.");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Import failed");
                _erorrs.Add($"Import failed - {ex.Message}");
                _step3Complete = false;
                _isImportingData = false;
                return;
            }
        }
        catch (Exception ex)
        {
            _erorrs.Add($"Export failed - {ex.Message}");
            _step3Complete = false;
            _isImportingData = false;
            return;
        }

        await SaveMappingChoices();

        _step3Complete = true;
        _isImportingData = false;
    }

    public async Task Clear()
    {
        _loadedFiles?.Clear();

        _step1Complete = false;
        _step2Complete = false;
        _step3Complete = false;
        _conflictsResolved = false;

        _stepIndex = 0;

        _erorrs.Clear();
        _rawPreview.Clear();
        _headers.Clear();
        _selectedPostingDateHeader = null;
        _selectedValueChangeHeader = null;
        _selectedTickerHeader = null;
        _mappedPreview.Clear();
        _summaryInfos.Clear();
        _warnings.Clear();

        _uploadedContent = null;
        _fileName = null;
        _fileSize = 0;
        _totalRowCount = 0;
        _importResult = null;

        try
        {
            _regenCts?.Cancel();
            _regenCts?.Dispose();
            _regenCts = null;
        }
        catch { }

        await Task.CompletedTask;
    }

    private void OnConflictsSubmitted()
    {
        _conflictsResolved = true;
    }

    private void SetDragClass() => _dragClass = $"{_defaultDragClass} mud-border-primary";
    private void ClearDragClass() => _dragClass = _defaultDragClass;

    private async Task OnPrimaryClick()
    {
        if (_stepIndex == 1)
        {
            _stepIndex = 2;
            await BeginImport();
        }
        else if (_stepIndex == 2 && _resolverRef is not null)
        {
            await _resolverRef.SubmitAsync();
        }
        else
        {
            _stepIndex++;
        }
    }

    private void GoToPreviousStep()
    {
        if (_stepIndex > 0)
            _stepIndex--;
    }

    private async Task OnPreviewInteraction(StepperInteractionEventArgs arg)
    {
        if (arg.Action == StepAction.Complete)
            await ControlStepCompletion(arg);
        else if (arg.Action == StepAction.Activate)
            await ControlStepNavigation(arg);
    }

    private async Task ControlStepCompletion(StepperInteractionEventArgs arg)
    {
        _erorrs.Clear();
        switch (arg.StepIndex)
        {
            case 0:
                if (_step1Complete != true)
                {
                    _erorrs.Add($"Can not continue. Select csv file");
                    arg.Cancel = true;
                }
                break;
            case 1:
                if (_step2Complete != true)
                    arg.Cancel = true;
                break;
            case 2:
                if (_step3Complete != true)
                    arg.Cancel = true;
                break;
        }
        await Task.CompletedTask;
    }

    private async Task ControlStepNavigation(StepperInteractionEventArgs arg)
    {
        switch (arg.StepIndex)
        {
            case 1:
                if (_step1Complete != true)
                {
                    arg.Cancel = true;
                }
                break;
            case 2:
                if (_step2Complete != true)
                {
                    arg.Cancel = true;
                }
                break;
            case 3:
                if (_step3Complete != true)
                {
                    arg.Cancel = true;
                }
                break;
        }
        await Task.CompletedTask;
    }

}
