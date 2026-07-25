using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.Dashboard;

/// <summary>
/// Read/query service that composes the dashboard first-paint data from the
/// existing per-card services into a single <see cref="DashboardOverviewDto"/>.
/// The underlying services share a scoped EF Core DbContext, which is not
/// thread-safe, so the queries are awaited sequentially rather than in parallel.
/// </summary>
public class DashboardQueryService(
    INetWorthService netWorthService,
    ILabelsValueService labelsValueService,
    IBalanceService balanceService,
    IAssetsService assetsService,
    ILiabilitiesService liabilitiesService,
    IExpenseDistributionService expenseDistributionService,
    ICurrencyRepository currencyRepository,
    IFinancialAccountRepository financialAccountRepository) : IDashboardQueryService
{
    public async Task<DashboardOverviewDto?> GetOverview(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        using var readScope = financialAccountRepository.BeginReadScope();
        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return null;

        var netWorth = await netWorthService.GetNetWorth(userId, currency, start, end);
        var netCashFlow = await balanceService.GetNetCashFlow(userId, currency, start, end);
        var closingBalance = await balanceService.GetClosingBalance(userId, currency, start, end);
        var liabilitiesPerType = await liabilitiesService.GetEndLiabilitiesPerType(userId, start, end).ToListAsync(cancellationToken);
        var liabilitiesPerAccount = await liabilitiesService.GetEndLiabilitiesPerAccount(userId, start, end).ToListAsync(cancellationToken);
        var labelsValue = await labelsValueService.GetLabelsValue(userId, start, end);
        var assetsPerType = await assetsService.GetEndAssetsPerType(userId, currency, end).ToListAsync(cancellationToken);
        var assetsPerAccount = await assetsService.GetEndAssetsPerAccount(userId, currency, end).ToListAsync(cancellationToken);
        var expenseDistribution = await expenseDistributionService.GetExpenseDistribution(userId, currency, start, end);

        return new DashboardOverviewDto
        {
            UserId = userId,
            CurrencyId = currencyId,
            StartDate = start,
            EndDate = end,
            NetWorthSeries = netWorth.OrderBy(x => x.Key)
                                     .Select(x => new TimeSeriesModel(x.Key, x.Value))
                                     .ToList(),
            NetCashFlowSeries = netCashFlow,
            ClosingBalanceSeries = closingBalance,
            LiabilitiesPerType = liabilitiesPerType,
            LiabilitiesPerAccount = liabilitiesPerAccount,
            LabelsValue = labelsValue,
            AssetsPerType = assetsPerType,
            AssetsPerAccount = assetsPerAccount,
            ExpenseDistribution = expenseDistribution,
        };
    }
}