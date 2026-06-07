using Blazored.LocalStorage;
using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.App;

public partial class LandingPage : ComponentBase
{
    private MudTheme _theme = new();

    [Inject] public required ILocalStorageService LocalStorageService { get; set; }
    [Inject] public required NewVisitorsHttpClient NewVisitorsHttpClient { get; set; }
    [Inject] public required ILogger<LandingPage> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Visit tracking must never block or break the landing page render.
        try
        {
            if (!await LocalStorageService.ContainKeyAsync("isThisFirstVisit") ||
                 await LocalStorageService.GetItemAsync<bool>("isThisFirstVisit"))
            {
                await LocalStorageService.SetItemAsync("isThisFirstVisit", false);
                await NewVisitorsHttpClient.AddVisit();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to record first visit.");
        }
    }

    async Task LogGuest()
    {
        await LoginService.Login("Guest", "GuestPassword");
        Navigation.NavigateTo("");
    }
}