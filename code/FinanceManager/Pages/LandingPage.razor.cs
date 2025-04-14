using FinanceManager.Application.Providers;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages;
public partial class LandingPage
{
    private MudTheme Theme = new();

    [Inject] public required PricingProvider PricingProvider { get; set; }

    async Task LogGuest()
    {
        await LoginService.Login("Guest", "GuestPassword");
        Navigation.NavigateTo("");
    }
}