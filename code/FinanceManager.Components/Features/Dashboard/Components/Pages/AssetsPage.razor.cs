using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Helpers;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Pages;

public partial class AssetsPage : ComponentBase
{
    private static readonly DashboardGridCard[] _cards =
    [
        new("assets-history", "Assets value over time", new(12, 12)),
        new("assets-distribution", "Assets distribution", new(4, 6)),
        new("portfolio-return", "Portfolio return", new(6, 12)),
        new("return-attribution", "Return attribution", new(4, 6)),
        new("investment-paycheck", "Investment paycheck", new(4, 6)),
        new("investment-rate", "Investment rate", new(4, 6)),
        new("diversification", "Diversification", new(6, 6)),
        new("fee-drag", "ETF fee drag", new(6, 6)),
    ];
    private AssetsPageCardsRefreshContext? _returnsContext;
    private Task<PortfolioReturnSourceModel>? _returnsRequest;
    [Inject] public required AssetsPageCardsCacheService AssetsCache { get; set; }

    private Task<PortfolioReturnSourceModel> GetPortfolioReturns(AssetsPageCardsRefreshContext context)
    {
        if (_returnsRequest is null || _returnsContext?.UserId != context.UserId
            || _returnsContext.CurrencyId != context.CurrencyId
            || _returnsContext.StartDateTime != context.StartDateTime
            || _returnsContext.EndDateTime != context.EndDateTime)
        {
            _returnsContext = context;
            _returnsRequest = AssetsCache.GetFreshReturnsAsync(context);
        }
        return _returnsRequest;
    }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    protected override void OnInitialized()
    {
        // Same default range as the Dashboard, from the one shared entry point. #700
        var (Start, End) = DateRangeHelper.GetDefaultOverviewRange(DateTime.UtcNow);

        StartDate = Start;
        EndDate = End;

        base.OnInitialized();
    }

    public void DateChanged((DateTime Start, DateTime End) changed)
    {
        _returnsRequest = null;
        StartDate = changed.Start;
        EndDate = changed.End;
        StateHasChanged();
    }
}