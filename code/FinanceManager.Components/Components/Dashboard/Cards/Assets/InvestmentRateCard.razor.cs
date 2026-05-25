using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class InvestmentRateCard
{
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    public List<InvestmentRate> InvestmentRates { get; set; } = [];
    private InvestmentRate? LatestInvestmentRate => InvestmentRates.FirstOrDefault(x => x.Salary != 0);

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;


    [Inject] public required ILogger<InvestmentRateCard> Logger { get; set; }
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
        try
        {
            InvestmentRates.Clear();

            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            try
            {
                var context = new AssetsPageCardsRefreshContext
                {
                    UserId = user.UserId,
                    CurrencyId = _currency.Id,
                    StartDateTime = StartDateTime,
                    EndDateTime = EndDateTime,
                };

                var snapshot = await AssetsPageCardsCacheService.GetSnapshotAsync(context);
                InvestmentRates = [.. snapshot.InvestmentRates];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while getting net worth");
            }
        }
        catch (Exception)
        {

            throw;
        }
        _isLoading = false;
    }

    private static string FormatPercentage(decimal value) => $"{value * 100m:0.00}%";

    private string FormatAmount(decimal value) => $"{value:0.00} {_currency.ShortName}";
}