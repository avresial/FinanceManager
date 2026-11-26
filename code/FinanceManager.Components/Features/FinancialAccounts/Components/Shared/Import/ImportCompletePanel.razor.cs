using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;

public partial class ImportCompletePanel : ComponentBase
{
    [Parameter] public EventCallback Clear { get; set; }
}