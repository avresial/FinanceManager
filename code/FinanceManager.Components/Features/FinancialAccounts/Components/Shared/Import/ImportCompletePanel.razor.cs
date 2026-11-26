using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;

public partial class ImportCompletePanel : ComponentBase
{
    [Parameter] public string ReturnHref { get; set; } = string.Empty;
    [Parameter] public string ReturnText { get; set; } = string.Empty;
    [Parameter] public string AccountName { get; set; } = string.Empty;
    [Parameter] public EventCallback Clear { get; set; }
}