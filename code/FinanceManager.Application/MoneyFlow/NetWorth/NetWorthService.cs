using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared;

namespace FinanceManager.Application.MoneyFlow.NetWorth;

/// <summary>
/// Net worth of the owner's pooled transactions: cash per currency (native sum, as before), bonds per definition and
/// investments per listing, whether or not an entry is linked to an account.
/// </summary>
public class NetWorthService(
    ICashTransactionReader cashTransactionReader,
    IBondTransactionReader bondTransactionReader,
    BondDashboardContext bondDashboardContext,
    IInvestmentValuationService investmentValuationService) : INetWorthService
{
    public async Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date)
    {
        date = DashboardReadCutoff.Clamp(date, DateTime.UtcNow);

        var cash = (await cashTransactionReader.GetPositions(userId, date)).Sum(p => p.Value);

        // Only the definitions the held bonds actually reference are loaded (detached, no-tracking),
        // and prices memoize in the request-scoped context so the dashboard's other bond paths reuse them.
        var bondPositions = await bondTransactionReader.GetPositions(userId, date);
        await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, bondPositions);
        var priceDate = DateOnly.FromDateTime(date);
        var bondValuation = new PooledBondValuation(bondDashboardContext, priceDate);
        var bonds = bondPositions.Sum(p => bondValuation.ValueOn(p, priceDate));

        var investments = (await investmentValuationService.GetUserValueByListingAsync(userId, currency, date)).Values.Sum();

        return Math.Round(cash + bonds + investments, 2);
    }

    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (start == new DateTime()) return [];
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var cashWalk = new OwnerLedgerWalk(await cashTransactionReader.GetValueRange(userId, start, end));
        var bondWalk = new OwnerLedgerWalk(await bondTransactionReader.GetValueRange(userId, start, end));
        await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, bondWalk);

        // One batched call prices each distinct listing once across all of the owner's trades,
        // rather than a per-day round-trip on the shared AppDbContext (which EF Core cannot service concurrently anyway).
        var investmentValues = await investmentValuationService.GetUserValueSeriesAsync(userId, currency, start.Date, end.Date);

        // Each distinct bond (anchor, units) combination is accrued once through the range end and read per day.
        var bondValuation = new PooledBondValuation(bondDashboardContext, DateOnly.FromDateTime(end));

        Dictionary<DateTime, decimal> result = [];
        for (var instant = end; instant >= start; instant = instant.AddDays(-1))
        {
            var priceDate = DateOnly.FromDateTime(instant);
            var dailyTotal = cashWalk.At(instant).Sum(p => p.Value)
                + bondWalk.At(instant).Sum(p => bondValuation.ValueOn(p, priceDate))
                + investmentValues.GetValueOrDefault(instant.Date);

            // Valued at the walk-back instant (so today includes today's entries) but keyed by
            // calendar day, so the series shares the day grid of the other dashboard series.
            result.Add(instant.Date, Math.Round(dailyTotal, 2));
        }
        return result;
    }
}