using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;

public abstract class ImportEntriesComponentBase<TAccount, TComponent> : ComponentBase
    where TAccount : BasicAccountInformation
{
    private const string _defaultDragClass = "relative rounded-lg border-2 border-dashed pa-4 mud-width-full mud-height-full";

    protected string _dragClass = _defaultDragClass;
    protected List<IBrowserFile> _loadedFiles = [];
    protected List<string> _erorrs = [];
    protected List<string> _warnings = [];
    protected List<List<string>> _rawPreview = [];
    protected List<string> _headers = [];
    protected string? _uploadedContent;
    protected string? _fileName;
    protected long _fileSize;
    protected int _totalRowCount;
    protected bool _isImportingData;
    protected int _stepIndex;
    protected bool _step1Complete;
    protected bool _step2Complete;
    protected bool _step3Complete;

    private CancellationTokenSource? _regenCts;
    private string _delimiterBacking = ",";

    public string AccountName { get; set; } = string.Empty;

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<TComponent> Logger { get; set; }
    [Inject] public required CsvHeaderMappingHttpClient MappingHttpClient { get; set; }

    protected string Delimiter
    {
        get => _delimiterBacking;
        set
        {
            if (value == _delimiterBacking)
                return;

            _delimiterBacking = value;
            ResetPreviewRegeneration();
            _ = RegeneratePreviewFromContentAsync(_regenCts!.Token);
        }
    }

    protected string PrimaryLabel => _stepIndex switch
    {
        0 => "Continue to mapping",
        1 => "Begin import",
        2 => "Begin import",
        _ => "Continue"
    };

    protected bool CanContinue => _stepIndex switch
    {
        0 => _rawPreview.Any() && _headers.Count >= MinimumHeaderCount,
        1 => !_erorrs.Any() && HasMappedPreview,
        2 => CanSubmitConflicts,
        _ => false
    };

    protected bool ShowPrimaryAction => _stepIndex switch
    {
        0 or 1 => true,
        2 => HasUnresolvedConflicts,
        _ => false
    };

    protected bool ShowBackAction => _stepIndex > 0 && _stepIndex < 2 && CanContinue;
    protected virtual bool ShowImportCompletedMessage => _stepIndex == 2 && _step3Complete && !HasUnresolvedConflicts;

    protected abstract int MinimumHeaderCount { get; }
    protected abstract bool HasMappedPreview { get; }
    protected abstract bool CanSubmitConflicts { get; }
    protected abstract bool HasUnresolvedConflicts { get; }
    protected abstract Task<(List<string> Headers, List<List<string>> Data)?> ReadCsvAsync(string content, string delimiter, CancellationToken cancellationToken);
    protected abstract void OnMappingChanged();
    protected abstract void ResetMappingState();
    protected abstract Task ApplySuggestedMappings();
    protected abstract Task BeginImport();
    protected abstract Task SubmitConflictsAsync();

    protected async Task LoadAccountName()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
                throw new Exception("User is null");

            var existingAccount = await FinancialAccountService.GetAccount<TAccount>(
                user.UserId,
                AccountId,
                DateTime.UtcNow,
                DateTime.UtcNow);

            if (existingAccount is not null)
                AccountName = existingAccount.Name;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }
    }

    protected async Task RegeneratePreviewFromContentAsync(CancellationToken cancellationToken = default)
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
            var preview = await ImportCsvPreviewReader.ReadAsync(_uploadedContent, Delimiter, ReadCsvAsync, cancellationToken);
            if (preview is null)
                return;

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
            Logger.LogDebug(ex, "CsvHelper attempt failed for delimiter {delimiter}", Delimiter);
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

    protected async Task UploadFiles(IBrowserFile? file)
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

            ResetPreviewRegeneration();
            await RegeneratePreviewFromContentAsync(_regenCts!.Token);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read uploaded file.");
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

    public virtual async Task Clear()
    {
        _loadedFiles.Clear();
        _step1Complete = false;
        _step2Complete = false;
        _step3Complete = false;
        _stepIndex = 0;

        _erorrs.Clear();
        _rawPreview.Clear();
        _headers.Clear();
        _warnings.Clear();
        ResetMappingState();

        _uploadedContent = null;
        _fileName = null;
        _fileSize = 0;
        _totalRowCount = 0;

        DisposePreviewRegeneration();
        await Task.CompletedTask;
    }

    protected async Task ApplySuggestedMappings(
        Action<HeaderMappingResultDto> applySuggestion,
        string failureMessage = "Failed to get mapping suggestions")
    {
        try
        {
            if (_headers.Count == 0)
                return;

            var suggestions = await MappingHttpClient.GetSuggestedMappingsAsync(_headers);
            if (suggestions is null || suggestions.Count == 0)
                return;

            foreach (var suggestion in suggestions)
                applySuggestion(suggestion);

            OnMappingChanged();
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, failureMessage);
        }
    }

    protected async Task SaveMappingChoices(
        IReadOnlyCollection<HeaderMappingRequestItemDto> mappingItems,
        string successMessage,
        string failureMessage)
    {
        try
        {
            if (mappingItems.Count == 0)
                return;

            await MappingHttpClient.SaveMappingsAsync(new SaveMappingRequestDto(mappingItems.ToList()));
            Logger.LogInformation(successMessage);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, failureMessage);
        }
    }

    protected void SetDragClass() => _dragClass = $"{_defaultDragClass} mud-border-primary";
    protected void ClearDragClass() => _dragClass = _defaultDragClass;

    protected async Task OnPrimaryClick()
    {
        if (_stepIndex == 1)
        {
            _stepIndex = 2;
            await BeginImport();
        }
        else if (_stepIndex == 2)
        {
            await SubmitConflictsAsync();
        }
        else
        {
            _stepIndex++;
        }
    }

    protected void GoToPreviousStep()
    {
        if (_stepIndex > 0)
            _stepIndex--;
    }

    protected async Task OnPreviewInteraction(StepperInteractionEventArgs arg)
    {
        if (arg.Action == StepAction.Complete)
            await ControlStepCompletion(arg);
        else if (arg.Action == StepAction.Activate)
            await ControlStepNavigation(arg);
    }

    protected async Task ControlStepCompletion(StepperInteractionEventArgs arg)
    {
        _erorrs.Clear();

        switch (arg.StepIndex)
        {
            case 0:
                if (!_step1Complete)
                {
                    _erorrs.Add("Can not continue. Select csv file");
                    arg.Cancel = true;
                }
                break;
            case 1:
                if (!_step2Complete)
                    arg.Cancel = true;
                break;
            case 2:
                if (!_step3Complete)
                    arg.Cancel = true;
                break;
        }

        await Task.CompletedTask;
    }

    protected async Task ControlStepNavigation(StepperInteractionEventArgs arg)
    {
        switch (arg.StepIndex)
        {
            case 1:
                if (!_step1Complete)
                    arg.Cancel = true;
                break;
            case 2:
                if (!_step2Complete)
                    arg.Cancel = true;
                break;
            case 3:
                if (!_step3Complete)
                    arg.Cancel = true;
                break;
        }

        await Task.CompletedTask;
    }

    protected void DisposePreviewRegeneration()
    {
        try
        {
            _regenCts?.Cancel();
            _regenCts?.Dispose();
            _regenCts = null;
        }
        catch
        {
        }
    }

    private void ResetPreviewRegeneration()
    {
        DisposePreviewRegeneration();
        _regenCts = new CancellationTokenSource();
    }
}