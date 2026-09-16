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

public partial class PortfolioReturnAttributionCard
{
    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;
    private PortfolioReturnAttributionResult? _result;

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "380px";

    [Inject] public required ILogger<PortfolioReturnAttributionCard> Logger { get; set; }
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

            _result = (await AssetsPageCardsCacheService.GetSnapshotAsync(context)).ReturnAttribution;
        }
        catch (Exception exception)
        {
            _hasError = true;
            Logger.LogError(exception, "Error getting portfolio return attribution");
        }
        finally
        {
            _isLoading = false;
        }
    }

    internal string UnsupportedComponentsText => _result is { UnsupportedComponents.Count: > 0 }
        ? string.Join("; ", _result.UnsupportedComponents.Select(component => component.Name))
        : "none";

    internal string StatusLabel => _result?.Status switch
    {
        PortfolioReturnAttributionStatus.InsufficientData => "Insufficient data",
        _ => "Attribution unavailable",
    };

    internal string StatusText => _result?.Status switch
    {
        PortfolioReturnAttributionStatus.InsufficientData => "Add investment history or select a range with portfolio activity.",
        _ => "Prices or historical exchange rates are missing for this range.",
    };

    internal string RangeText => $"{StartDateTime.ToString("d", CultureInfo.InvariantCulture)} – {EndDateTime.ToString("d", CultureInfo.InvariantCulture)}";

    private string FormatAmount(decimal? value)
    {
        if (value is not decimal amount)
            return "—";

        var sign = amount > 0m ? "+" : string.Empty;
        return $"{sign}{amount.ToString("N2", CultureInfo.CurrentCulture)} {_currency.ShortName}";
    }
}