using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards.Assets;

public partial class AssetsDistributionOverviewCard
{
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _typeData = [];
    private List<NameValueResult> _walletData = [];

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<AssetsDistributionOverviewCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override void OnInitialized()
    {
        _currency = SettingsService.GetCurrency();
    }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _typeData = [];
                _walletData = [];
                return;
            }

            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = _currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await AssetsPageCardsCacheService.GetSnapshotAsync(context);
            _typeData = [.. snapshot.EndAssetsPerType];
            _walletData = [.. snapshot.EndAssetsPerAccount];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading assets distribution");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}