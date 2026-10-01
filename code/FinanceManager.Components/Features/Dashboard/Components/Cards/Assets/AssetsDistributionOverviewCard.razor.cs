using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class AssetsDistributionOverviewCard : IDisposable
{
    private readonly RefreshVersionGate _gate = new();
    private bool _isLoading;
    private bool _hasError;
    private bool _hasDisplayedModel;
    private (int UserId, int CurrencyId, DateTime StartDate, DateTime EndDate)? _displayScope;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _typeData = [];
    private List<NameValueResult> _walletData = [];

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it hydrates its own snapshot and always refreshes.
    [Parameter] public DistributionCardModel? Model { get; set; }

    [Inject] public required ILogger<AssetsDistributionOverviewCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var version = _gate.Claim();
        var start = StartDateTime.Date;
        var end = EndDateTime;
        try
        {
            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            if (Model is not null)
            {
                _currency = currency;
                _hasError = false;
                _displayScope = null;
                await ShowData(Model, null);
                return;
            }

            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                _currency = currency;
                _hasError = false;
                _displayScope = null;
                await ShowData(new DistributionCardModel([], []), null);
                return;
            }

            var scope = (user.UserId, currency.Id, start, end.Date);
            var sameScope = _displayScope == scope && _hasDisplayedModel;
            _currency = currency;
            _hasError = false;
            if (!sameScope)
            {
                _displayScope = scope;
                _hasDisplayedModel = false;
                _typeData = [];
                _walletData = [];
                _isLoading = true;
                StateHasChanged();
            }

            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = start,
                EndDateTime = end,
            };
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<AssetsDistributionSnapshot, DistributionCardModel>
            {
                Key = $"assets-distribution:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDate == start && snapshot.AsOfDate.Date == end.Date
                    ? new DistributionCardModel(snapshot.TypeData, snapshot.AccountData) : null,
                FetchAsync = async () => await AssetsPageCardsCacheService.GetFreshDistributionAsync(context),
                ToSnapshot = model => new AssetsDistributionSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    StartDate = start,
                    AsOfDate = end,
                    TypeData = model.TypeData,
                    AccountData = model.AccountData,
                },
                OnSnapshotPainted = model => ShowData(model, scope),
                OnSnapshotMissing = () =>
                {
                    if (!sameScope)
                    {
                        _typeData = [];
                        _walletData = [];
                        _isLoading = true;
                    }
                    StateHasChanged();
                    return Task.CompletedTask;
                },
                OnRefreshed = model => ShowData(model, scope),
            });
            if (!_gate.IsCurrent(version)) return;
            _hasError = result.IsBlockingFailure && !_hasDisplayedModel;
            _isLoading = false;
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(version)) return;
            _hasError = true;
            _isLoading = false;
            Logger.LogError(ex, "Error resolving assets distribution context");
        }
    }

    private Task ShowData(DistributionCardModel model, (int UserId, int CurrencyId, DateTime StartDate, DateTime EndDate)? scope)
    {
        _typeData = [.. model.TypeData];
        _walletData = [.. model.AccountData];
        _displayScope = scope;
        _hasDisplayedModel = true;
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void Dispose() => _gate.Claim();
}