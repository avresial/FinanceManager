using Blazored.LocalStorage;
using FinanceManager.Components.HttpContexts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages;

public partial class LandingPage
{
    private MudTheme _theme = new();

    [Inject] public required ILocalStorageService LocalStorageService { get; set; }
    [Inject] public required NewVisitorsHttpContext NewVisitorsHttpContext { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!await LocalStorageService.ContainKeyAsync("isThisFirstVisit") ||
             await LocalStorageService.GetItemAsync<bool>("isThisFirstVisit"))
        {
            await LocalStorageService.SetItemAsync("isThisFirstVisit", false);
            await NewVisitorsHttpContext.AddVisit();
        }
    }

    async Task LogGuest()
    {
        await LoginService.Login("Guest", "GuestPassword");
        Navigation.NavigateTo("");
    }
}