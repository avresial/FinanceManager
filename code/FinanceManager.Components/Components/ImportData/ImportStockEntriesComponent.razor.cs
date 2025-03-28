using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Readers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Components.ImportData
{
    public partial class ImportStockEntriesComponent
    {
        private const string DefaultDragClass = "relative rounded-lg border-2 border-dashed pa-4 mt-4 mud-width-full mud-height-full";
        private string _dragClass = DefaultDragClass;

        private CsvConfiguration config = new CsvConfiguration(new CultureInfo("de-DE"))
        {
            Delimiter = ";",
            HasHeaderRecord = true,
        };

        private List<ImportStockModel> importStockModels = new();
        private List<ImportStockExtendedModel> importStockExtendedModels = new();

        public List<IBrowserFile> LoadedFiles = new();
        private List<string> _erorrs = new();
        private List<string> _warnings = new();
        private List<string> _summaryInfos = new();

        private int _stepIndex;
        private bool _datasetCointainsAdditionalInformation = false;
        private bool _isImportingData = false;

        private bool _step1Complete;
        private bool _step2Complete;
        private bool _step3Complete;

        private bool _isFormValid;
        private bool _isTouched;

        private string _exportTicker = string.Empty;
        private string _postingDateHeader = "PostingDate";
        private string _valueChangeHeader = "ValueChange";
        private string _tickerHeader = "Ticker";
        private string _investmentTypeHeader = "InvestmentType";

        [Parameter]
        public required int AccountId { get; set; }

        [Inject]
        public required IFinancialAccountService FinancalAccountService { get; set; }

        public async Task UploadFiles(InputFileChangeEventArgs e)
        {
            _isImportingData = true;

            importStockModels.Clear();
            importStockExtendedModels.Clear();

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

            if (_datasetCointainsAdditionalInformation)
            {
                try
                {
                    importStockExtendedModels = await ImportStockExtendedModelReader.Read(config, file, _postingDateHeader, _valueChangeHeader, _tickerHeader, _investmentTypeHeader);
                }
                catch (HeaderValidationException ex)
                {
                    Console.WriteLine(ex);
                    _erorrs.Add($"Invalid headers. Required headers:{_postingDateHeader}, {_valueChangeHeader},{_tickerHeader}, {_investmentTypeHeader}.");
                }
            }
            else
            {
                try
                {
                    importStockModels = await ImportStockModelReader.Read(config, file, _postingDateHeader, _valueChangeHeader);
                }
                catch (HeaderValidationException ex)
                {
                    Console.WriteLine(ex);
                    _erorrs.Add($"Invalid headers. Required headers:{_postingDateHeader}, {_valueChangeHeader}.");
                }
            }

            _step1Complete = importStockModels.Any() || importStockExtendedModels.Any();

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

            string ticker = _exportTicker;
            int importedEntriesCount = 0;
            if (importStockModels.Any())
            {
                foreach (var result in importStockModels)
                {
                    try
                    {
                        await FinancalAccountService.AddEntry(new StockAccountEntry(AccountId, -1, result.PostingDate, -1, result.ValueChange, _exportTicker, Domain.Enums.InvestmentType.Unknown));
                        importedEntriesCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        _warnings.Add(ex.Message);
                    }
                }
            }
            else if (importStockExtendedModels.Any())
            {
                foreach (var result in importStockExtendedModels)
                {
                    try
                    {
                        await FinancalAccountService.AddEntry(new StockAccountEntry(AccountId, -1, result.PostingDate, -1, result.ValueChange, result.Ticker, result.InvestmentType));
                        importedEntriesCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        _warnings.Add(ex.Message);
                    }
                }
            }
            if (importedEntriesCount > 0)
                _summaryInfos.Add($"Imported {importedEntriesCount} rows.");

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

        private void SetDragClass() => _dragClass = $"{DefaultDragClass} mud-border-primary";
        private void ClearDragClass() => _dragClass = DefaultDragClass;

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
}