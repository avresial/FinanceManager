using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class FeeDragCard : IDisposable
{
    private const decimal _minimumReturnRate = -0.10m;
    private const decimal _maximumReturnRate = 0.10m;
    private const decimal _returnRateStep = 0.005m;
    private const decimal _defaultReturnRate = 0.07m;
    private static readonly int[] _projectionHorizons = [10, 20, 30];

    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;
    private decimal _assumedAnnualReturnRate = _defaultReturnRate;
    private DateTime? _lastRequestedAsOfDate;
    private CancellationTokenSource? _loadCancellationTokenSource;

    internal FeeDragAnalysisResult? Analysis
    {
        get => _analysis;
        set => _analysis = value;
    }

    private FeeDragAnalysisResult? _analysis;

    [Parameter] public string Height { get; set; } = "360px";
    [Parameter] public DateTime AsOfDate { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<FeeDragCard> Logger { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    internal decimal AssumedAnnualReturnRate => _assumedAnnualReturnRate;

    internal IReadOnlyList<FeeDragProjection> ComputedProjections
    {
        get
        {
            if (_analysis is null || _analysis.TotalHoldingsCount == 0)
                return [];

            var principal = Math.Max(0m, _analysis.TotalHoldingsValue - _analysis.MissingTerHoldingsValue);
            var netRate = Math.Max(-1m, _assumedAnnualReturnRate - _analysis.WeightedExpenseRatio);

            return _projectionHorizons.Select(years =>
            {
                var gross = principal * (decimal)Math.Pow((double)(1m + _assumedAnnualReturnRate), years);
                var net = principal * (decimal)Math.Pow((double)(1m + netRate), years);
                var grossRounded = RoundMoney(gross);
                var netRounded = RoundMoney(net);

                return new FeeDragProjection
                {
                    Years = years,
                    GrossFutureValue = grossRounded,
                    NetFutureValue = netRounded,
                    CumulativeFeeCost = RoundMoney(Math.Max(0m, grossRounded - netRounded)),
                };
            }).ToList();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _currency = await SettingsService.GetCurrencyAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_lastRequestedAsOfDate == AsOfDate)
            return;

        _lastRequestedAsOfDate = AsOfDate;
        await LoadFeeDragDataAsync();
    }

    internal async Task LoadFeeDragDataAsync()
    {
        _loadCancellationTokenSource?.Cancel();
        var cancellationTokenSource = new CancellationTokenSource();
        _loadCancellationTokenSource = cancellationTokenSource;
        var asOfDate = AsOfDate;

        _isLoading = true;
        _hasError = false;

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!ReferenceEquals(_loadCancellationTokenSource, cancellationTokenSource))
                return;

            if (user is null)
            {
                _analysis = null;
                return;
            }

            var analysis = await AssetsHttpClient.GetFeeDragAnalysis(
                user.UserId,
                _currency,
                asOfDate,
                _assumedAnnualReturnRate,
                cancellationTokenSource.Token);

            if (ReferenceEquals(_loadCancellationTokenSource, cancellationTokenSource))
                _analysis = analysis;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(_loadCancellationTokenSource, cancellationTokenSource))
                return;

            _hasError = true;
            Logger.LogError(exception, "Error loading ETF fee drag analysis");
        }
        finally
        {
            if (ReferenceEquals(_loadCancellationTokenSource, cancellationTokenSource))
            {
                _isLoading = false;
                _loadCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    public void Dispose()
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource = null;
    }

    internal void OnReturnRateChanged(decimal value)
    {
        _assumedAnnualReturnRate = Math.Clamp(value, _minimumReturnRate, _maximumReturnRate);
        if (_analysis is not null)
            _analysis.AssumedAnnualReturnRate = _assumedAnnualReturnRate;
    }

    internal static string FormatPercentage(decimal value) => $"{value * 100m:0.0}%";

    private string FormatAmount(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    private string FormatCurrency(decimal value) => $"{FormatAmount(value)} {_currency.ShortName}";

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}