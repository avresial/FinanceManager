using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
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
    private readonly RefreshVersionGate _gate = new();
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
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }

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

    protected override async Task OnParametersSetAsync()
    {
        if (_lastRequestedAsOfDate == AsOfDate)
            return;

        _lastRequestedAsOfDate = AsOfDate;
        await LoadFeeDragDataAsync();
    }

    internal async Task LoadFeeDragDataAsync()
    {
        var version = _gate.Claim();
        _loadCancellationTokenSource?.Cancel();
        var cancellationTokenSource = new CancellationTokenSource();
        _loadCancellationTokenSource = cancellationTokenSource;
        var asOfDate = AsOfDate;

        _hasError = false;
        _isLoading = _analysis is null;

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version))
                return;

            if (user is null)
            {
                _analysis = null;
                _isLoading = false;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version))
                return;

            _currency = currency;
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<FeeDragSnapshot, FeeDragAnalysisResult>
            {
                Key = $"fee-drag:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                // The as-of date is metadata: never show an old analysis under a different date.
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.AsOfDate.Date == asOfDate.Date
                    ? Normalize(snapshot.Analysis) : null,
                // Always the default rate: the rendered facts must not depend on the slider.
                FetchAsync = async () =>
                {
                    var analysis = await AssetsHttpClient.GetFeeDragAnalysis(
                        user.UserId,
                        currency,
                        asOfDate,
                        _defaultReturnRate,
                        cancellationTokenSource.Token);
                    return analysis is null ? null : Normalize(analysis);
                },
                ToSnapshot = model => new FeeDragSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    AsOfDate = asOfDate,
                    Analysis = model,
                },
                OnSnapshotPainted = ShowAnalysis,
                OnSnapshotMissing = () =>
                {
                    _analysis = null;
                    _isLoading = true;
                    return InvokeAsync(StateHasChanged);
                },
                OnRefreshed = ShowAnalysis,
            });

            if (!_gate.IsCurrent(version))
                return;

            _hasError = result.IsBlockingFailure;
            _isLoading = false;
        }
        catch (Exception exception)
        {
            if (!_gate.IsCurrent(version))
                return;

            _analysis = null;
            _hasError = true;
            _isLoading = false;
            Logger.LogError(exception, "Error loading ETF fee drag analysis");
        }
    }

    public void Dispose()
    {
        // Supersede any in-flight run so it cannot render into a disposed component.
        _gate.Claim();
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource = null;
    }

    internal void OnReturnRateChanged(decimal value) =>
        _assumedAnnualReturnRate = Math.Clamp(value, _minimumReturnRate, _maximumReturnRate);

    private Task ShowAnalysis(FeeDragAnalysisResult analysis)
    {
        _analysis = analysis;
        _isLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    // Keeps only what the card renders; the projections are recomputed locally from the slider.
    private static FeeDragAnalysisResult Normalize(FeeDragAnalysisResult analysis) => new()
    {
        TotalHoldingsValue = analysis.TotalHoldingsValue,
        AnnualFeeCost = analysis.AnnualFeeCost,
        WeightedExpenseRatio = analysis.WeightedExpenseRatio,
        MissingTerCount = analysis.MissingTerCount,
        MissingTerHoldingsValue = analysis.MissingTerHoldingsValue,
        TotalHoldingsCount = analysis.TotalHoldingsCount,
        Holdings = [.. analysis.Holdings],
        Projections = [],
        AssumedAnnualReturnRate = 0m,
    };

    internal static string FormatPercentage(decimal value) => $"{value * 100m:0.0}%";

    private string FormatAmount(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    private string FormatCurrency(decimal value) => $"{FormatAmount(value)} {_currency.ShortName}";

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}