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

public partial class MoneyWeightedReturnCard
{
    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;
    private MoneyWeightedReturnResult? _result;

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<MoneyWeightedReturnCard> Logger { get; set; }
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

            _result = (await AssetsPageCardsCacheService.GetSnapshotAsync(context)).MoneyWeightedReturn;
        }
        catch (Exception ex)
        {
            _hasError = true;
            Logger.LogError(ex, "Error getting money-weighted return");
        }
        finally
        {
            _isLoading = false;
        }
    }

    internal static string FormatReturn(decimal annualizedReturn) =>
        $"{annualizedReturn * 100m:0.00}%";

    internal string StatusText => _result?.Status switch
    {
        MoneyWeightedReturnStatus.InsufficientData => "Not enough investment history for this range.",
        MoneyWeightedReturnStatus.Unavailable => "Return unavailable until prices and exchange rates are available.",
        MoneyWeightedReturnStatus.NoSolution => "No valid annualized return solution for this range.",
        _ => ""
    };

    internal string StatusLabel => _result?.Status switch
    {
        MoneyWeightedReturnStatus.InsufficientData => "Insufficient data",
        MoneyWeightedReturnStatus.NoSolution => "No solution",
        _ => "Unavailable"
    };

    internal string RangeText => $"{StartDateTime.ToString("d", CultureInfo.InvariantCulture)} – {EndDateTime.ToString("d", CultureInfo.InvariantCulture)}";
}