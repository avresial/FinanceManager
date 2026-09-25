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

public partial class PortfolioReturnAttributionCard
{
    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;
    private PortfolioReturnAttributionResult? _result;
    private readonly RefreshVersionGate _refreshGate = new();

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
        var version = _refreshGate.Claim();
        var startDateTime = StartDateTime;
        var endDateTime = EndDateTime;
        _isLoading = true;
        _hasError = false;

        var user = await LoginService.GetLoggedUser();
        if (!_refreshGate.IsCurrent(version)) return;
        if (user is null)
        {
            _result = null;
            _isLoading = false;
            return;
        }

        try
        {
            var currency = await SettingsService.GetCurrencyAsync();
            if (!_refreshGate.IsCurrent(version)) return;

            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = startDateTime,
                EndDateTime = endDateTime,
            };

            var result = (await AssetsPageCardsCacheService.GetSnapshotAsync(context)).ReturnAttribution;
            if (!_refreshGate.IsCurrent(version)) return;

            _currency = currency;
            _result = result;
        }
        catch (Exception exception)
        {
            if (_refreshGate.IsCurrent(version))
            {
                _hasError = true;
                Logger.LogError(exception, "Error getting portfolio return attribution");
            }
        }
        finally
        {
            if (_refreshGate.IsCurrent(version))
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

    private IReadOnlyList<AttributionStep> AttributionSteps
    {
        get
        {
            if (_result is null)
                return [];

            var components = new (string Label, decimal Value)[]
            {
                ("External cash movement", _result.ExternalCashMovement ?? 0m),
                ("Market / valuation effect", _result.MarketEffect ?? 0m),
                ("FX effect", _result.FxEffect ?? 0m),
                ("Known transaction fees", _result.FeeEffect ?? 0m),
            };

            var steps = new List<AttributionStep>(components.Length);
            var runningTotal = 0m;
            foreach (var component in components.OrderByDescending(component => component.Value))
            {
                var start = runningTotal;
                runningTotal += component.Value;
                steps.Add(new(component.Label, component.Value, start, runningTotal));
            }

            return steps;
        }
    }

    private string AttributionSummary => $"Starting at {FormatAmount(0m)}. "
        + string.Join(" ", AttributionSteps.Select(step =>
            $"{step.Label}: {FormatAmount(step.Value)}; running total {FormatAmount(step.End)}."))
        + $" Total change: {FormatAmount(_result?.TotalChange)}.";

    private decimal ChartPosition(decimal value, IReadOnlyList<AttributionStep> steps)
    {
        var minimum = Math.Min(0m, Math.Min(_result?.TotalChange ?? 0m, steps.Min(step => Math.Min(step.Start, step.End))));
        var maximum = Math.Max(0m, Math.Max(_result?.TotalChange ?? 0m, steps.Max(step => Math.Max(step.Start, step.End))));
        return minimum == maximum ? 50m : 4m + 92m * (value - minimum) / (maximum - minimum);
    }

    private string BarStyle(decimal start, decimal end, IReadOnlyList<AttributionStep> steps)
    {
        var left = ChartPosition(Math.Min(start, end), steps).ToString("0.###", CultureInfo.InvariantCulture);
        if (start == end)
            return $"left:{left}%;width:2px";

        var width = (ChartPosition(Math.Max(start, end), steps) - ChartPosition(Math.Min(start, end), steps))
            .ToString("0.###", CultureInfo.InvariantCulture);
        return $"left:{left}%;width:{width}%";
    }

    private string FormatAmount(decimal? value)
    {
        if (value is not decimal amount)
            return "—";

        var sign = amount > 0m ? "+" : string.Empty;
        return $"{sign}{amount.ToString("N2", CultureInfo.CurrentCulture)} {_currency.ShortName}";
    }

    private sealed record AttributionStep(string Label, decimal Value, decimal Start, decimal End);
}