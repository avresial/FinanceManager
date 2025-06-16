using Blazored.LocalStorage;
using FinanceManager.Application.Providers;
using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages;
public partial class LandingPage
{
    private MudTheme Theme = new();
    [Inject] public required ILocalStorageService LocalStorageService { get; set; }
    [Inject] public required PricingProvider PricingProvider { get; set; }
    [Inject] public required NewVisitorsService NewVisitorsService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!await LocalStorageService.ContainKeyAsync("isThisFirstVisit") ||
             await LocalStorageService.GetItemAsync<bool>("isThisFirstVisit"))
        {
            await LocalStorageService.SetItemAsync("isThisFirstVisit", false);
            await NewVisitorsService.AddVisit();
        }
    }

    async Task LogGuest()
    {
        await LoginService.Login("Guest", "GuestPassword");
        Navigation.NavigateTo("");
    }


}