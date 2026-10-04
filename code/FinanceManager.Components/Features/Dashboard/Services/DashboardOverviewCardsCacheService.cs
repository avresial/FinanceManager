using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Components.Features.Dashboard.Services;

public class DashboardOverviewCardsCacheService(MoneyFlowHttpClient moneyFlowHttpClient)
{
    private readonly Dictionary<(int UserId, int CurrencyId, DateTime Start, DateTime End), Task<DashboardOverviewCardsCacheSnapshot>> _freshRequests = [];

    // Share only overlapping requests. A later visit always starts a fresh request.
    public virtual async Task<DashboardOverviewCardsCacheSnapshot> GetFreshAsync(DashboardOverviewCardsRefreshContext context)
    {
        var key = (context.UserId, context.CurrencyId, context.StartDateTime.Date, context.EndDateTime);
        Task<DashboardOverviewCardsCacheSnapshot> request;
        lock (_freshRequests)
        {
            if (!_freshRequests.TryGetValue(key, out request!))
            {
                request = BuildStateAsync(context);
                _freshRequests.Add(key, request);
            }
        }
        try
        {
            return await request;
        }
        finally
        {
            lock (_freshRequests)
            {
                if (_freshRequests.TryGetValue(key, out var current) && ReferenceEquals(current, request))
                    _freshRequests.Remove(key);
            }
        }
    }

    private async Task<DashboardOverviewCardsCacheSnapshot> BuildStateAsync(DashboardOverviewCardsRefreshContext refreshContext)
    {
        var startDate = refreshContext.StartDateTime.Date;
        var endDate = refreshContext.EndDateTime;

        // Only the id crosses the wire, so the requested currency can be rebuilt from the context.
        var currency = new Currency { Id = refreshContext.CurrencyId };
        var netCashFlowTask = moneyFlowHttpClient.GetNetCashFlow(refreshContext.UserId, currency, startDate, endDate);
        var closingBalanceTask = moneyFlowHttpClient.GetClosingBalance(refreshContext.UserId, currency, startDate, endDate);

        await Task.WhenAll(netCashFlowTask, closingBalanceTask);

        var snapshot = new DashboardOverviewCardsCacheSnapshot
        {
            UserId = refreshContext.UserId,
            CurrencyId = refreshContext.CurrencyId,
            StartDateTime = startDate,
            EndDateTime = endDate,
            NetCashFlowSeries = [.. (await netCashFlowTask)],
            ClosingBalanceSeries = [.. (await closingBalanceTask)],
        };

        return snapshot;
    }
}