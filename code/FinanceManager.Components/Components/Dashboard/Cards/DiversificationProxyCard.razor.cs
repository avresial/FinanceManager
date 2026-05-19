using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class DiversificationProxyCard
{
    private bool _isLoading;
    private DiversificationScore? _score;
    private Color _bandColor = Color.Default;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<DiversificationProxyCard> Logger { get; set; }
    [Inject] public required DiversificationHttpClient DiversificationHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _score = null;
                return;
            }

            _score = await DiversificationHttpClient.GetDiversificationScore(user.UserId, DateTime.UtcNow);
            _bandColor = _score?.Band switch
            {
                "Broad" => Color.Success,
                "Moderate" => Color.Warning,
                _ => Color.Error
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading diversification score");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}
