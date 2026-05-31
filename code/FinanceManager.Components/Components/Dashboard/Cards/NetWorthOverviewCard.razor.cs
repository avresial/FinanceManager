using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class NetWorthOverviewCard
{
    private Currency _currency = DefaultCurrency.PLN;
    private decimal? _totalNetWorth = null;
    private bool _isLoading = false;
    private bool _hasError = false;

    // Exposed for the failure-path unit test (InternalsVisibleTo FinanceManager.UnitTests).
    internal bool HasError => _hasError;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<NetWorthOverviewCard> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required DashboardOverviewCardsCacheService DashboardOverviewCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => LoadAsync();

    internal async Task LoadAsync()
    {
        _isLoading = true;
        _hasError = false;

        _currency = SettingsService.GetCurrency();
        _totalNetWorth = null;

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            _isLoading = false;
            return;
        }

        try
        {
            var context = new DashboardOverviewCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = _currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await DashboardOverviewCardsCacheService.GetSnapshotAsync(context);
            _totalNetWorth = snapshot.NetWorth;
        }
        catch (Exception ex)
        {
            _hasError = true;
            Logger.LogError(ex, "Error while getting net worth");
            Snackbar.Add("Unable to load net worth.", Severity.Error);
        }

        _isLoading = false;
    }
}