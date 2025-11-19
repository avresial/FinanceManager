using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages;

public partial class LandingPage
{
    private MudTheme _theme = new();

    [Inject] public required ILocalStorageService LocalStorageService { get; set; }
    [Inject] public required NewVisitorsHttpClient NewVisitorsHttpClient { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!await LocalStorageService.ContainKeyAsync("isThisFirstVisit") ||
             await LocalStorageService.GetItemAsync<bool>("isThisFirstVisit"))
        {
            await LocalStorageService.SetItemAsync("isThisFirstVisit", false);
            await NewVisitorsHttpClient.AddVisit();
        }
    }

    async Task LogGuest()
    {
        await LoginService.Login("Guest", "GuestPassword");
        Navigation.NavigateTo("");
    }
}