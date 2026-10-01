using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class InvestmentPaycheckEstimatorCard : IDisposable
{
    private readonly RefreshVersionGate _gate = new();
    private InvestmentPaycheckSourceModel? _source;
    private Currency _currency = DefaultCurrency.PLN;
    private bool _isLoading = true;
    private bool _hasError;
    private bool _disposed;
    private int? _sourceUserId;
    private int? _sourceCurrencyId;
    private int? _sourceSalaryMonths;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int SalaryMonths { get; set; } = 3;

    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ILogger<InvestmentPaycheckEstimatorCard> Logger { get; set; }

    protected override Task OnParametersSetAsync() => RefreshSourceAsync();

    private async Task RefreshSourceAsync()
    {
        var version = _gate.Claim();
        var salaryMonths = SalaryMonths;
        _hasError = false;
        if (_source is not null && _sourceSalaryMonths != salaryMonths)
            ClearSource();
        _isLoading = _source is null;
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!IsCurrent(version)) return;

            if (user is null)
            {
                ClearSource();
                _hasError = true;
                _isLoading = false;
                StateHasChanged();
                return;
            }

            if (_source is not null && _sourceUserId != user.UserId)
            {
                ClearSource();
                _isLoading = true;
                StateHasChanged();
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!IsCurrent(version)) return;

            if (_source is not null && (_sourceUserId != user.UserId
                || _sourceCurrencyId != currency.Id
                || _sourceSalaryMonths != salaryMonths))
            {
                ClearSource();
                _isLoading = true;
                StateHasChanged();
            }

            _currency = currency;
            var asOfDateUtc = DateTime.UtcNow;
            var key = $"investment-paycheck-source:{user.UserId}:{currency.Id}:{salaryMonths}";
            var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<InvestmentPaycheckSourceSnapshot, InvestmentPaycheckSourceModel>
            {
                Key = key,
                ContentComparer = EqualityComparer<InvestmentPaycheckSourceModel>.Default,
                Gate = _gate,
                ClaimedVersion = version,
                ToModel = snapshot => snapshot.UserId == user.UserId
                    && snapshot.CurrencyId == currency.Id
                    && snapshot.SalaryMonths == salaryMonths
                    ? snapshot.Model
                    : null,
                FetchAsync = async () =>
                {
                    // The endpoint currently requires a rate, but its result is projected to rate-independent source facts.
                    var estimate = await AssetsHttpClient.GetInvestmentPaycheckEstimate(
                        user.UserId, currency, asOfDateUtc, withdrawalRate: 0.04m, salaryMonths: salaryMonths);
                    return estimate is null ? null : InvestmentPaycheckSourceModel.FromEstimate(estimate);
                },
                ToSnapshot = model => new InvestmentPaycheckSourceSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    SalaryMonths = salaryMonths,
                    AsOfDateUtc = asOfDateUtc,
                    Model = model,
                },
                OnSnapshotPainted = model => ShowSourceAsync(version, user.UserId, currency.Id, salaryMonths, model),
                OnSnapshotMissing = () => ShowLoadingAsync(version),
                OnRefreshed = model => ShowSourceAsync(version, user.UserId, currency.Id, salaryMonths, model),
            });

            if (!IsCurrent(version)) return;

            if (result.Outcome is SnapshotRefreshOutcome.Empty or SnapshotRefreshOutcome.Failed)
            {
                _hasError = _source is null;
                _isLoading = false;
                StateHasChanged();
                if (result.Error is not null)
                    Logger.LogError(result.Error, "Unable to load investment paycheck source data.");
            }
        }
        catch (Exception exception)
        {
            if (!IsCurrent(version)) return;

            _isLoading = false;
            _hasError = _source is null;
            Logger.LogError(exception, "Unable to load investment paycheck source data.");
            StateHasChanged();
        }
    }

    private Task ShowSourceAsync(int version, int userId, int currencyId, int salaryMonths, InvestmentPaycheckSourceModel source)
    {
        if (!IsCurrent(version)) return Task.CompletedTask;

        _source = source;
        _sourceUserId = userId;
        _sourceCurrencyId = currencyId;
        _sourceSalaryMonths = salaryMonths;
        _isLoading = false;
        _hasError = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task ShowLoadingAsync(int version)
    {
        if (!IsCurrent(version)) return Task.CompletedTask;

        _isLoading = _source is null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private bool IsCurrent(int version) => !_disposed && _gate.IsCurrent(version);

    private void ClearSource()
    {
        _source = null;
        _sourceUserId = null;
        _sourceCurrencyId = null;
        _sourceSalaryMonths = null;
    }

    public void Dispose()
    {
        _disposed = true;
        _gate.Claim();
    }
}