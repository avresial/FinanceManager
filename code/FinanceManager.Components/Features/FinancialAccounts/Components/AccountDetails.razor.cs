using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.FinancialAccounts.Components;

public partial class AccountDetails
{
    [Parameter, SupplyParameterFromQuery(Name = "entryId")]
    public int? EntryId { get; set; }

    /// <summary><c>?dialog=add-entry</c> opens the add-entry form, so the state can be linked to directly.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "dialog")]
    public string? Dialog { get; set; }

    public const string AddEntryDialog = "add-entry";

    private bool OpensAddEntry => string.Equals(Dialog, AddEntryDialog, StringComparison.OrdinalIgnoreCase);
}