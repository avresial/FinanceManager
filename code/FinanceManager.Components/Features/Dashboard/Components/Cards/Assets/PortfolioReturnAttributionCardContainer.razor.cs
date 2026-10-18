using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class PortfolioReturnAttributionCardContainer
{
    private readonly RefreshVersionGate _gate = new();
    private PortfolioReturnAttributionCardModel? _model;
    private Currency _currency = DefaultCurrency.PLN;
    private AssetsPageCardsRefreshContext? _paintedContext;
    private bool _isLoading;
    private bool _hasError;

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "380px";
    [Parameter] public Func<AssetsPageCardsRefreshContext, Task<PortfolioReturnSourceModel>>? FetchReturns { get; set; }

    // Reports whether a reload for the selected range is in flight, so the page can show the refresh indicator.
    [Parameter] public EventCallback<bool> RefreshingChanged { get; set; }

    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }
    [Inject] public required AssetsPageCardsRequestCoordinator AssetsRequests { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<PortfolioReturnAttributionCardContainer> Logger { get; set; }

    protected override Task OnParametersSetAsync() => Reload();
    private Task Retry() => Reload(forceRefresh: true);

    private async Task Reload(bool forceRefresh = false)
    {
        var version = _gate.Claim();
        var start = StartDateTime.Date;
        var end = EndDateTime;
        _isLoading = _model is null;
        _hasError = false;
        try
        {
            await RefreshingChanged.InvokeAsync(true);
            var user = await LoginService.GetLoggedUser();
            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                _model = null;
                _paintedContext = null;
                return;
            }
            if (_paintedContext?.UserId != user.UserId || _paintedContext.CurrencyId != currency.Id)
            {
                _model = null;
                _paintedContext = null;
            }
            _isLoading = _model is null;
            _currency = currency;
            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = start,
                EndDateTime = end,
            };
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<PortfolioReturnAttributionCardSnapshot, PortfolioReturnAttributionCardModel>
            {
                Key = $"portfolio-return-attribution:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDateTime == start && snapshot.EndDateTime.Date == end.Date ? snapshot.Model : null,
                FetchAsync = async () =>
                {
                    var source = await (forceRefresh || FetchReturns is null
                        ? AssetsRequests.GetFreshReturnsAsync(context) : FetchReturns(context));
                    if (source.Attribution is null)
                        throw new InvalidOperationException("The portfolio attribution request failed.");
                    return PortfolioReturnAttributionCardModel.FromResult(source.Attribution);
                },
                ToSnapshot = model => new PortfolioReturnAttributionCardSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    StartDateTime = start,
                    EndDateTime = end,
                    Model = model,
                    FetchedAtUtc = DateTime.UtcNow,
                },
                OnSnapshotPainted = model => Paint(model, context),
                OnRefreshed = model => Paint(model, context),
            });
            if (_gate.IsCurrent(version)) _hasError = result.Error is not null;
        }
        catch (Exception exception)
        {
            if (_gate.IsCurrent(version))
            {
                _hasError = true;
                Logger.LogError(exception, "Could not load portfolio return attribution");
            }
        }
        finally
        {
            if (_gate.IsCurrent(version))
            {
                _isLoading = false;
                await RefreshingChanged.InvokeAsync(false);
            }
        }
    }

    private Task Paint(PortfolioReturnAttributionCardModel model, AssetsPageCardsRefreshContext context)
    {
        _model = model;
        _paintedContext = context;
        _isLoading = false;
        return InvokeAsync(StateHasChanged);
    }
}