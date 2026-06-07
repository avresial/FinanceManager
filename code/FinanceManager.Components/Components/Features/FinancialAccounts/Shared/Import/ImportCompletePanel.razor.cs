using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.Shared.Import;

public partial class ImportCompletePanel : ComponentBase
{
    [Parameter] public int AccountId { get; set; }
    [Parameter] public string AccountName { get; set; } = string.Empty;
    [Parameter] public EventCallback Clear { get; set; }
}