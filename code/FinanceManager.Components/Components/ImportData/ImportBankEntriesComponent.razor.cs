using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Components.ImportData;

public partial class ImportBankEntriesComponent : ComponentBase
{
    private const string _defaultDragClass = "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";
    private string _dragClass = _defaultDragClass;
    private List<ImportBankModel> _importModels = [];

    private List<IBrowserFile> LoadedFiles = [];
    private List<string> _erorrs = [];
    private List<string> _warnings = [];
    private List<string> _summaryInfos = [];

    private List<List<string>> _rawPreview = [];
    private List<string> _headers = [];
    private string? _selectedPostingDateHeader;
    private string? _selectedValueChangeHeader;
    private List<(DateTime PostingDate, decimal ValueChange)> _mappedPreview = [];

    private ImportResult? _importResult = null;
    private string? _uploadedContent;

    private CancellationTokenSource? _regenCts;

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

    private bool _isFormValid;
    private bool _isTouched;

    public required string AccountName { get; set; }

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<ImportBankEntriesComponent> Logger { get; set; }
    [Inject] public required BankAccountHttpContext BankAccountHttpContext { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) throw new Exception("User is null");

            var existingAccount = await FinancialAccountService.GetAccount<BankAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);
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
            var result = await ImportBankModelReader.Read(_uploadedContent, _delimiter, cancellationToken);

            _headers = result.Value.Headers ?? [];
            var allParsedRows = result.Value.Data ?? [];

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

        _importModels.Clear();
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

        LoadedFiles = [file];
        if (file is null)
        {
            _erorrs.Add("Failed to load file.");
            _isImportingData = false;
            return;
        }

        await Clear();

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

        if (string.IsNullOrWhiteSpace(_selectedPostingDateHeader) || string.IsNullOrWhiteSpace(_selectedValueChangeHeader))
        {
            _step2Complete = false;
            return;
        }

        try
        {
            _mappedPreview = GetExportData(_selectedPostingDateHeader, _selectedValueChangeHeader, _headers, _rawPreview).ToList();
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

        _stepIndex = 2;
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

            var (Headers, Data) = await ImportBankModelReader.Read(_uploadedContent, _delimiter, CancellationToken.None) ??
                throw new Exception("Failed to read data for import.");

            var exportResult = GetExportData(_selectedPostingDateHeader, _selectedValueChangeHeader, Headers, Data);
            var entries = exportResult.Select(x => new BankEntryImportRecordDto(x.PostingDate.ToUniversalTime(), x.ValueChange)).ToList();

            try
            {
                _importResult = await BankAccountHttpContext.ImportBankEntriesAsync(new(AccountId, entries));

                if (_importResult is not null && _importResult.Imported != 0)
                    _summaryInfos.Add($"Imported {_importResult.Imported} entries.");

                if (_importResult is not null && _importResult.Conflicts.Count != 0)
                {
                    var exactMatches = _importResult.Conflicts.Count(x => x.IsExactMatch);
                    var exactMatchesDays = _importResult.Conflicts.Where(x => !x.IsExactMatch)
                        .DistinctBy(x => x.DateTime.Date)
                        .Count();

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
        LoadedFiles?.Clear();

        _step1Complete = false;
        _step2Complete = false;
        _step3Complete = false;

        _stepIndex = 0;

        _erorrs.Clear();
        _rawPreview.Clear();
        _headers.Clear();
        _selectedPostingDateHeader = null;
        _selectedValueChangeHeader = null;
        _mappedPreview.Clear();
        _summaryInfos.Clear();
        _warnings.Clear();

        _uploadedContent = null;

        try
        {
            _regenCts?.Cancel();
            _regenCts?.Dispose();
            _regenCts = null;
        }
        catch { }

        await Task.CompletedTask;
    }

    private void SetDragClass() => _dragClass = $"{_defaultDragClass} mud-border-primary";
    private void ClearDragClass() => _dragClass = _defaultDragClass;
    private void GoToNextStep() => _stepIndex++;
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
    private IEnumerable<(DateTime PostingDate, decimal ValueChange)> GetExportData(string postingDateHeader, string valueChangeHeader,
           List<string> headers, List<List<string>> dataToConvert)
    {
        var postingIndex = headers.FindIndex(h => h.Equals(postingDateHeader, StringComparison.OrdinalIgnoreCase));
        var valueIndex = headers.FindIndex(h => h.Equals(valueChangeHeader, StringComparison.OrdinalIgnoreCase));

        if (postingIndex < 0 || valueIndex < 0)
            throw new Exception("Selected headers are invalid.");

        foreach (var row in dataToConvert)
        {
            var posting = postingIndex < row.Count ? row[postingIndex] : string.Empty;
            var value = valueIndex < row.Count ? row[valueIndex] : string.Empty;

            if (!DateTime.TryParse(posting, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new Exception($"Could not parse posting date: '{posting}'");

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueChange))
                throw new Exception($"Could not parse value change: '{value}'");

            yield return (date, valueChange);
        }
    }
}