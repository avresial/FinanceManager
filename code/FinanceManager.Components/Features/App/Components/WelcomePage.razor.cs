using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.App.Components;

public partial class WelcomePage : ComponentBase
{
    [Inject] public required NavigationManager Navigation { get; set; }

    /// <param name="kind">cash, bond or investment: the kind the add dialog on Transactions opens on.</param>
    private void AddTransaction(string kind) => Navigation.NavigateTo($"Transactions?dialog=add-entry&kind={kind}");

    private void ImportCsv() => Navigation.NavigateTo("Transactions/Import/cash");

    private void ImportBondCsv() => Navigation.NavigateTo("Transactions/Import/bond");
}