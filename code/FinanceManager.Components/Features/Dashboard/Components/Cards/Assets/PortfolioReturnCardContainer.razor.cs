using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class PortfolioReturnCardContainer
{
    private readonly RefreshVersionGate _gate = new();
    private PortfolioReturnCardModel? _model;
    private Currency _currency = DefaultCurrency.PLN;
    private bool _isLoading;

    [Parameter] public string? Height { get; set; }
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsCache { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<PortfolioReturnCardContainer> Logger { get; set; }

    [Parameter] public Func<AssetsPageCardsRefreshContext, Task<PortfolioReturnSourceModel>>? FetchReturns { get; set; }

    protected override Task OnParametersSetAsync() => Reload();
    private Task Retry() => Reload(forceRefresh: true);

    private async Task Reload(bool forceRefresh = false)
    {
        var version = _gate.Claim();
        var start = StartDateTime.Date;
        var end = EndDateTime;
        _model = null;
        _isLoading = true;
        try
        {
            var user = await LoginService.GetLoggedUser();
            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version) || user is null) return;
            _currency = currency;
            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = start,
                EndDateTime = end,
            };
            PortfolioReturnSourceModel? fallback = null;
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<PortfolioReturnCardSnapshot, PortfolioReturnCardModel>
            {
                Key = $"portfolio-return:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDateTime == start && snapshot.EndDateTime.Date == end.Date ? snapshot.Model : null,
                FetchAsync = async () =>
                {
                    fallback = await (forceRefresh || FetchReturns is null
                        ? AssetsCache.GetFreshReturnsAsync(context) : FetchReturns(context));
                    if (fallback.MoneyWeightedReturn is null || fallback.TimeWeightedReturn is null || fallback.Attribution is null)
                        throw new InvalidOperationException("A portfolio return request failed.");
                    return PortfolioReturnCardModel.FromSource(fallback);
                },
                ToSnapshot = model => new PortfolioReturnCardSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    StartDateTime = start,
                    EndDateTime = end,
                    Model = model,
                    FetchedAtUtc = DateTime.UtcNow,
                },
                OnSnapshotPainted = Paint,
                OnRefreshed = Paint,
            });
            if (_gate.IsCurrent(version) && result.IsBlockingFailure)
                _model = fallback is null ? null : PortfolioReturnCardModel.FromSource(fallback);
        }
        catch (Exception exception)
        {
            if (_gate.IsCurrent(version)) Logger.LogError(exception, "Could not load portfolio returns");
        }
        finally
        {
            if (_gate.IsCurrent(version)) _isLoading = false;
        }
    }

    private Task Paint(PortfolioReturnCardModel model)
    {
        _model = model;
        _isLoading = false;
        return InvokeAsync(StateHasChanged);
    }
}