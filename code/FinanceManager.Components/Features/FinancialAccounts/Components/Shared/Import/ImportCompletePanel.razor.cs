using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;

public partial class ImportCompletePanel : ComponentBase
{
    [Parameter] public int AccountId { get; set; }
    [Parameter] public string AccountName { get; set; } = string.Empty;
    [Parameter] public EventCallback Clear { get; set; }
}