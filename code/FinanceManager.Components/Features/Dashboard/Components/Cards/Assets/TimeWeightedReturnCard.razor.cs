using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class TimeWeightedReturnCard
{
    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;
    private TimeWeightedReturnResult? _result;

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<TimeWeightedReturnCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            _result = null;
            return;
        }

        _isLoading = true;
        _hasError = false;
        try
        {
            _currency = await SettingsService.GetCurrencyAsync();
            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = _currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            _result = (await AssetsPageCardsCacheService.GetSnapshotAsync(context)).TimeWeightedReturn;
        }
        catch (Exception ex)
        {
            _hasError = true;
            Logger.LogError(ex, "Error getting time-weighted return");
        }
        finally
        {
            _isLoading = false;
        }
    }

    internal static string FormatReturn(decimal totalReturn) =>
        $"{totalReturn * 100m:0.00}%";

    internal string StatusText => _result?.Status switch
    {
        TimeWeightedReturnStatus.InsufficientData => "Not enough portfolio history for this range.",
        TimeWeightedReturnStatus.Unavailable => "Return unavailable until prices and exchange rates are available.",
        _ => ""
    };

    internal string StatusLabel => _result?.Status switch
    {
        TimeWeightedReturnStatus.InsufficientData => "Insufficient data",
        _ => "Unavailable"
    };

    internal string RangeText => $"{StartDateTime.ToString("d", CultureInfo.InvariantCulture)} – {EndDateTime.ToString("d", CultureInfo.InvariantCulture)}";
}