using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
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

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the cache service as in standalone usage.
    [Parameter] public DistributionCardModel? Model { get; set; }

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
            if (Model is not null)
            {
                _typeData = [.. Model.TypeData];
                _walletData = [.. Model.AccountData];
                return;
            }

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