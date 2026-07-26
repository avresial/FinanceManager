using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.App.Components;

public partial class WelcomePage : ComponentBase
{
    [Inject] public required NavigationManager Navigation { get; set; }

    private void AddAccount(string type) => Navigation.NavigateTo($"AddAccount?type={type}");
}