using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Components.ImportData;

public partial class ImportBankEntriesComponent : ComponentBase
{
    private const string _defaultDragClass = "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";
    private string _dragClass = _defaultDragClass;

    private CsvConfiguration _config = new(new CultureInfo("de-DE"))
    {
        Delimiter = ";",
        HasHeaderRecord = true,
    };

    private List<ImportBankModel> _importModels = [];

    private List<IBrowserFile> LoadedFiles = [];
    private List<string> _erorrs = [];
    private List<string> _warnings = [];
    private List<string> _summaryInfos = [];

    private int _stepIndex;
    private bool _isImportingData;

    private bool _step1Complete;
    private bool _step2Complete;
    private bool _step3Complete;

    private bool _isFormValid;
    private bool _isTouched;

    private string _postingDateHeader = "PostingDate";
    private string _valueChangeHeader = "ValueChange";

    public required string AccountName { get; set; }

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ILoginService loginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var user = await loginService.GetLoggedUser();
        var existingAccount = await FinancialAccountService.GetAccount<BankAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);
        if (existingAccount is not null)
            AccountName = existingAccount.Name;
    }

    public async Task UploadFiles(InputFileChangeEventArgs e)
    {
        _isImportingData = true;

        _importModels.Clear();

        _erorrs.Clear();
        if (Path.GetExtension(e.File.Name) != ".csv")
        {
            _erorrs.Add($"{e.File.Name} is not a csv file. Select csv file to continue.");
            _isImportingData = false;
            return;
        }

        LoadedFiles = e.GetMultipleFiles(1).ToList();

        var file = LoadedFiles.FirstOrDefault();
        if (file is null) return;

        try
        {
            _importModels = await ImportBankModelReader.Read(_config, file, _postingDateHeader, _valueChangeHeader);
        }
        catch (HeaderValidationException ex)
        {
            Console.WriteLine(ex);
            _erorrs.Add($"Invalid headers. Required headers:{_postingDateHeader}, {_valueChangeHeader}.");
        }

        _step1Complete = _importModels.Any();

        if (_step1Complete)
        {
            _stepIndex++;
        }
        else
        {
            _erorrs.Add("Step 1 can not be completed - loading files failed.");
        }
        _isImportingData = false;
    }
    public async Task BeginImport()
    {
        _isImportingData = true;

        int totalEntries = _importModels.Count;
        int importedEntriesCount = 0;

        if (_importModels.Count != 0)
        {
            foreach (var result in _importModels.GroupBy(x => x.PostingDate.Date))
            {
                foreach (var (index, entry) in result.Index())
                {
                    try
                    {
                        await FinancialAccountService.AddEntry(new BankAccountEntry(AccountId, -1, entry.PostingDate.AddSeconds(index), -1, entry.ValueChange));
                        importedEntriesCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        _warnings.Add(ex.Message);
                    }
                }
            }
        }

        if (importedEntriesCount > 0)
        {
            _summaryInfos.Add($"Imported {importedEntriesCount} rows.");

            if (importedEntriesCount < totalEntries)
                _warnings.Add($"Failed to import {totalEntries - importedEntriesCount} rows. Check warnings for details.");
        }
        else if (totalEntries > 0)
        {
            _warnings.Add("Failed to import any entries. Check warnings for details.");
        }
        else
        {
            _warnings.Add("No entries to import.");
        }

        StateHasChanged();

        _step2Complete = true;
        _isImportingData = false;
        _stepIndex++;
    }
    public async Task Clear()
    {
        if (LoadedFiles is not null)
            LoadedFiles.Clear();

        _step1Complete = false;
        _step2Complete = false;
        _step3Complete = false;

        _stepIndex = 0;

        _erorrs.Clear();
        await Task.CompletedTask;
    }

    private void SetDragClass() => _dragClass = $"{_defaultDragClass} mud-border-primary";
    private void ClearDragClass() => _dragClass = _defaultDragClass;

    private async Task OnPreviewInteraction(StepperInteractionEventArgs arg)
    {
        if (arg.Action == StepAction.Complete)
        {
            await ControlStepCompletion(arg);
        }
        else if (arg.Action == StepAction.Activate)
        {
            await ControlStepNavigation(arg);
        }
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
                {
                    arg.Cancel = true;
                }
                break;
            case 2:
                if (_step3Complete != true)
                {
                    arg.Cancel = true;
                }
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