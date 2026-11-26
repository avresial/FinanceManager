using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Transactions.Import.Components;

public partial class ImportCompletePanel : ComponentBase
{
    [Parameter] public EventCallback Clear { get; set; }
}