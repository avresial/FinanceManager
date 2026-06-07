using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.FinancialAccounts.Shared.Import;

public partial class ImportStepperFooter : ComponentBase
{
    [Parameter] public bool DisableClear { get; set; }
    [Parameter] public bool ShowBackAction { get; set; }
    [Parameter] public bool ShowPrimaryAction { get; set; }
    [Parameter] public bool CanContinue { get; set; }
    [Parameter] public string PrimaryLabel { get; set; } = "Continue";
    [Parameter] public EventCallback Clear { get; set; }
    [Parameter] public EventCallback Back { get; set; }
    [Parameter] public EventCallback PrimaryClick { get; set; }
}
