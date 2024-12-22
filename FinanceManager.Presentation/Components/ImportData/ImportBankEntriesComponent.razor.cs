using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace FinanceManager.Presentation.Components.ImportData
{
    public partial class ImportBankEntriesComponent : ComponentBase
    {
        private List<string> _erorrs = new();
        private List<string> _warnings = new();
        private List<string> _summaryInfos = new();

        private bool _step1Complete;
        private bool _step2Complete;
        private bool _step3Complete;

        private int _index;

        public List<IBrowserFile> LoadedFiles = new();

        [Parameter]
        public required string AccountName { get; set; }

        [Inject]
        public required IAccountService AccountService { get; set; }
        public void BeginImport()
        {
            //       _isImportingData = true;

            int importedEntriesCount = 0;


            _summaryInfos.Add($"Imported {importedEntriesCount} rows.");

            StateHasChanged();

            _step2Complete = true;
            //     _isImportingData = false;
            _index++;
        }
        public async Task Clear()
        {
            if (LoadedFiles is not null)
                LoadedFiles.Clear();

            _step1Complete = false;
            _step2Complete = false;
            _step3Complete = false;
            _index = 0;
        }
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
        }
    }
}