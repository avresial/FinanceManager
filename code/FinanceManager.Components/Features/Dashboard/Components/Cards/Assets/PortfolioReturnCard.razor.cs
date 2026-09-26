using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class PortfolioReturnCard
{
    private readonly RefreshVersionGate _refreshGate = new();
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private MoneyWeightedReturnResult? _moneyWeightedReturn;
    private TimeWeightedReturnResult? _timeWeightedReturn;
    private PortfolioReturnAttributionResult? _attribution;

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<PortfolioReturnCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private Task Retry() => Reload(forceRefresh: true);

    private async Task Reload(bool forceRefresh = false)
    {
        var version = _refreshGate.Claim();
        var startDateTime = StartDateTime;
        var endDateTime = EndDateTime;
        _isLoading = true;

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_refreshGate.IsCurrent(version)) return;
            if (user is null)
            {
                _moneyWeightedReturn = null;
                _timeWeightedReturn = null;
                _attribution = null;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!_refreshGate.IsCurrent(version)) return;
            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = startDateTime,
                EndDateTime = endDateTime,
            };
            var snapshot = forceRefresh
                ? await AssetsPageCardsCacheService.RefreshAsync(context)
                : await AssetsPageCardsCacheService.GetSnapshotAsync(context);
            if (!_refreshGate.IsCurrent(version)) return;

            _currency = currency;
            _moneyWeightedReturn = snapshot.MoneyWeightedReturn;
            _timeWeightedReturn = snapshot.TimeWeightedReturn;
            _attribution = snapshot.ReturnAttribution;
        }
        catch (Exception exception)
        {
            if (_refreshGate.IsCurrent(version))
            {
                _moneyWeightedReturn = null;
                _timeWeightedReturn = null;
                _attribution = null;
                Logger.LogError(exception, "Could not load portfolio returns");
            }
        }
        finally
        {
            if (_refreshGate.IsCurrent(version)) _isLoading = false;
        }
    }

    internal static string FormatReturn(decimal value) => $"{value * 100m:0.00}%";

    internal string RangeText => $"{StartDateTime.ToString("d", CultureInfo.InvariantCulture)} – {EndDateTime.ToString("d", CultureInfo.InvariantCulture)}";
    internal int PeriodLength => Math.Max(0, (EndDateTime.Date - StartDateTime.Date).Days + 1);
    internal bool HasAvailableReturn => _moneyWeightedReturn?.IsAvailable == true || _timeWeightedReturn?.IsAvailable == true;
    internal decimal? ExternalCashMovement => _attribution?.IsAvailable == true ? _attribution.ExternalCashMovement : null;

    internal string InsightText => ExternalCashMovement switch
    {
        > 0m => $"Net external inflows of {FormatAmount(ExternalCashMovement.Value)} occurred during this period. Cash-flow timing affects XIRR; TWR neutralizes it.",
        < 0m => $"Net external outflows of {FormatAmount(-ExternalCashMovement.Value)} occurred during this period. Cash-flow timing affects XIRR; TWR neutralizes it.",
        _ => "Cash-flow timing affects annualized XIRR; cumulative TWR neutralizes external cash flows."
    };

    internal string MoneyWeightedStatusLabel => _moneyWeightedReturn?.Status switch
    {
        MoneyWeightedReturnStatus.InsufficientData => "Insufficient data",
        MoneyWeightedReturnStatus.NoSolution => "No solution",
        _ => "Unavailable",
    };

    internal string MoneyWeightedStatusText => _moneyWeightedReturn?.Status switch
    {
        MoneyWeightedReturnStatus.InsufficientData => "Not enough investment history for this range.",
        MoneyWeightedReturnStatus.NoSolution => "No valid annualized return solution for this range.",
        MoneyWeightedReturnStatus.Unavailable => "Prices or exchange rates are missing.",
        _ => "Could not load this return.",
    };

    internal string TimeWeightedStatusLabel => _timeWeightedReturn?.Status switch
    {
        TimeWeightedReturnStatus.InsufficientData => "Insufficient data",
        _ => "Unavailable",
    };

    internal string TimeWeightedStatusText => _timeWeightedReturn?.Status switch
    {
        TimeWeightedReturnStatus.InsufficientData => "Not enough portfolio history for this range.",
        TimeWeightedReturnStatus.Unavailable => "Prices or exchange rates are missing.",
        _ => "Could not load this return.",
    };

    private string FormatAmount(decimal value) => $"{value.ToString("N2", CultureInfo.CurrentCulture)} {_currency.ShortName}";
}