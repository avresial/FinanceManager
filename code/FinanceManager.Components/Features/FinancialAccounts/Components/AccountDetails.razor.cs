using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components;

public partial class AccountDetails
{
    [Parameter, SupplyParameterFromQuery(Name = "entryId")]
    public int? EntryId { get; set; }
}