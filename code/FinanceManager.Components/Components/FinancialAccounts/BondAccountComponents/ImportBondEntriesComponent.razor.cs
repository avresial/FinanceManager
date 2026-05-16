using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Components.FinancialAccounts.BondAccountComponents;

public partial class ImportBondEntriesComponent : ComponentBase
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
    private string? _selectedBondHeader;
    private List<(DateTime PostingDate, decimal ValueChange, int BondDetailsId, string BondName)> _mappedPreview = [];

    private BondImportResult? _importResult;
    private string? _uploadedContent;
    private string? _fileName;
    private long _fileSize;
    private int _totalRowCount;
    private bool _conflictsResolved;
    private CancellationTokenSource? _regenCts;

    private List<BondDetails> _possibleBonds = [];

    private string _delimiterBacking = ",";
    private string _delimiter
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
    [Inject] public required ILogger<ImportBondEntriesComponent> Logger { get; set; }
    [Inject] public required BondAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }

    private BondEntryConflictResolver? _resolverRef;
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
            _possibleBonds = await BondDetailsHttpClient.GetAll();

            var user = await LoginService.GetLoggedUser();
            if (user is null)
                throw new Exception("User is null");

            var existingAccount = await FinancialAccountService.GetAccount<BondAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);
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
            var result = await ImportStockModelReader.Read(_uploadedContent, _delimiter, cancellationToken);

            _headers = result.Value.Headers ?? [];
            var allParsedRows = result.Value.Data ?? [];
            _totalRowCount = allParsedRows.Count;

            if (_headers.Count != 0 && allParsedRows.Count != 0)
                _rawPreview = allParsedRows.Take(3).ToList();

            var emptyIndexes = _rawPreview.First().Where(x => string.IsNullOrEmpty(x)).Select(x => _rawPreview.First().IndexOf(x)).ToList();
            foreach (var previewItem in _rawPreview.Skip(1))
            {
                var newEmptyIndexes = previewItem.Where(x => string.IsNullOrEmpty(x)).Select(x => previewItem.IndexOf(x)).ToList();
                var indexToRemove = emptyIndexes.Where(x => !newEmptyIndexes.Any(y => y == x)).ToList();
                emptyIndexes.RemoveAll(x => indexToRemove.Any(y => y == x));
            }

            foreach (var index in emptyIndexes.OrderByDescending(x => x))
            {
                _headers.RemoveAt(index);
                foreach (var previewItem in _rawPreview)
                    previewItem.RemoveAt(index);
            }

        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "CsvHelper attempt failed for delimiter {delimiter}", _delimiter);
        }

        if (_headers.Count == 0)
            _erorrs.Add("No headers found in CSV.");

        _step1Complete = _rawPreview.Count != 0;

        if (!_step1Complete && _erorrs.Count == 0)
            _erorrs.Add("Step 1 can not be completed - loading files failed.");

        await InvokeAsync(StateHasChanged);
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

        if (!Path.GetExtension(file.Name).Equals(".csv", StringComparison.InvariantCultureIgnoreCase))
        {
            _erorrs.Add($"{file.Name} is not a csv file. Select csv file to continue.");
            _isImportingData = false;
            return;
        }

        _loadedFiles = [file];
        if (file is null)
        {
            _erorrs.Add("Failed to load file.");
            _isImportingData = false;
            return;
        }

        await Clear();

        _fileName = file.Name;
        _fileSize = file.Size;

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
            using StreamReader reader = new(stream);

            var content = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(content))
            {
                _erorrs.Add("File is empty.");
                _isImportingData = false;
                return;
            }

            _uploadedContent = content;

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
            string.IsNullOrWhiteSpace(_selectedBondHeader))
        {
            _step2Complete = false;
            return;
        }

        try
        {
            _mappedPreview = GetExportData(_selectedPostingDateHeader, _selectedValueChangeHeader, _selectedBondHeader, _headers, _rawPreview).ToList();
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

            if (string.IsNullOrEmpty(_selectedBondHeader))
                throw new Exception("Bond column is not selected.");

            var (headers, data) = await ImportStockModelReader.Read(_uploadedContent, _delimiter, CancellationToken.None)
                ?? throw new Exception("Failed to read data for import.");

            var exportResult = GetExportData(_selectedPostingDateHeader, _selectedValueChangeHeader, _selectedBondHeader, headers, data);
            var entries = exportResult.Select(x => new BondEntryImportRecordDto(x.PostingDate, x.ValueChange, x.BondDetailsId)).ToList();

            try
            {
                _importResult = await AccountImportHttpClient.ImportBondEntriesAsync(new(AccountId, entries));

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
        _selectedBondHeader = null;
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
                    _erorrs.Add("Can not continue. Select csv file");
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
                    arg.Cancel = true;
                break;
            case 2:
                if (_step2Complete != true)
                    arg.Cancel = true;
                break;
            case 3:
                if (_step3Complete != true)
                    arg.Cancel = true;
                break;
        }

        await Task.CompletedTask;
    }

    private IEnumerable<(DateTime PostingDate, decimal ValueChange, int BondDetailsId, string BondName)> GetExportData(
        string postingDateHeader, string valueChangeHeader, string bondHeader,
        List<string> headers, List<List<string>> dataToConvert)
    {
        var postingIndex = headers.FindIndex(h => h.Equals(postingDateHeader, StringComparison.OrdinalIgnoreCase));
        var valueIndex = headers.FindIndex(h => h.Equals(valueChangeHeader, StringComparison.OrdinalIgnoreCase));
        var bondIndex = headers.FindIndex(h => h.Equals(bondHeader, StringComparison.OrdinalIgnoreCase));

        if (postingIndex < 0 || valueIndex < 0 || bondIndex < 0)
            throw new Exception("Selected headers are invalid.");

        foreach (var row in dataToConvert)
        {
            var posting = postingIndex < row.Count ? row[postingIndex] : string.Empty;
            var value = valueIndex < row.Count ? row[valueIndex] : string.Empty;
            var bond = bondIndex < row.Count ? row[bondIndex] : string.Empty;

            var allowedFormats = new[]
            {
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy H:mm:ss",
                "MM/dd/yyyy HH:mm:ss",
                "MM/dd/yyyy H:mm:ss",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy/MM/dd HH:mm:ss"
            };

            if (!DateTime.TryParseExact(posting, allowedFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                throw new Exception($"Could not parse posting date: '{posting}'");

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueChange))
                throw new Exception($"Could not parse value change: '{value}'");

            if (string.IsNullOrWhiteSpace(bond))
                throw new Exception("Bond value is empty.");

            var resolvedBond = ResolveBond(bond);
            if (resolvedBond is null)
                throw new Exception($"Could not map bond '{bond}' to existing bond details (id or name).");

            yield return (new(date.Ticks, DateTimeKind.Utc), valueChange, resolvedBond.Id, resolvedBond.Name);
        }
    }

    private BondDetails? ResolveBond(string bondValue)
    {
        var normalized = bondValue.Trim();
        if (int.TryParse(normalized, out var id))
            return _possibleBonds.FirstOrDefault(x => x.Id == id);

        return _possibleBonds.FirstOrDefault(x => string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
