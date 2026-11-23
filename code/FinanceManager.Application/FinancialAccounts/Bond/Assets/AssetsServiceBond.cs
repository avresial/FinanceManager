using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Extensions;
using FinanceManager.Domain.FinancialAccounts.Bond.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared;

namespace FinanceManager.Application.FinancialAccounts.Bond.Assets;

/// <summary>
/// Bond assets are the pooled bond units per definition: all of a user's entries of one bond add up whatever account they belong to,
/// and interest accrues from the latest pooled entry. Unrealized gain/loss follows the same pooled positions.
/// </summary>
public class AssetsServiceBond(
    IBondTransactionReader bondTransactionReader,
    BondDashboardContext bondDashboardContext,
    IBondUnrealizedGainLossCalculator bondUnrealizedGainLossCalculator) : IAssetsServiceTyped
{
    private const string _bondTypeName = "Bond";
    private const string _bondsGroupName = "Bonds";

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        if (start == new DateTime()) return [];

        var walk = new OwnerLedgerWalk(await bondTransactionReader.GetValueRange(userId, start, end));
        if (walk.IsEmpty) return [];

        await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, walk);
        var valuation = new PooledBondValuation(bondDashboardContext, DateOnly.FromDateTime(end));

        // Every day carries an explicit value so a bond sold to zero does not keep yesterday's asset value.
        return [.. walk.EachDayEnd(start, end).Select(day => new TimeSeriesModel(
            day.Day,
            day.Positions.Where(p => p.Value > 0m).Sum(p => valuation.ValueOn(p, DateOnly.FromDateTime(day.Day)))))];
    }

    public Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType) =>
        investmentType == InvestmentType.Bond ? GetAssetsTimeSeries(userId, currency, start, end) : Task.FromResult(new List<TimeSeriesModel>());

    public async Task<AssetsDistributionResult> GetEndAssetsDistribution(
        int userId, Currency currency, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var held = (await bondTransactionReader.GetPositions(userId, asOfDate, cancellationToken)).Where(p => p.Value > 0m).ToList();
        if (held.Count == 0) return new([], []);

        await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, held, cancellationToken);
        var date = DateOnly.FromDateTime(asOfDate);
        var valuation = new PooledBondValuation(bondDashboardContext, date);
        var perBond = held
            .Select(p => new NameValueResult(bondDashboardContext.GetDetails(p.PartitionId).Name, valuation.ValueOn(p, date)))
            .Where(r => r.Value > 0m)
            .ToList();

        return new(perBond.Count == 0 ? [] : [new(_bondTypeName, perBond.Sum(r => r.Value))], perBond);
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        foreach (var row in (await GetEndAssetsDistribution(userId, currency, asOfDate)).PerAccount)
            yield return row;
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        foreach (var row in (await GetEndAssetsDistribution(userId, currency, asOfDate)).PerType)
            yield return row;
    }

    public async Task<bool> IsAnyAccountWithAssets(int userId) =>
        (await bondTransactionReader.GetPositions(userId, DateTime.UtcNow)).Any(p => p.Value > 0m);

    public async Task<List<UnrealizedGainLossAccountResult>> GetUnrealizedGainLossPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var instruments = await GetUnrealizedGainLossPerInstrument(userId, currency, asOfDate);
        if (instruments.Count == 0) return [];

        // Pooled bonds belong to the owner, not to an account: one aggregate row without an account id.
        var included = instruments.Where(x => !x.IsExcludedFromTotals).ToList();
        var costBasis = included.Sum(x => x.CostBasis);
        var currentValue = included.Sum(x => x.CurrentValue);
        var gainLoss = new GainLoss(costBasis, currentValue);

        return [new UnrealizedGainLossAccountResult(
            null,
            _bondsGroupName,
            costBasis,
            currentValue,
            gainLoss.Amount,
            gainLoss.Percent,
            asOfDate,
            instruments.Count - included.Count)];
    }

    public async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate)
    {
        var held = (await bondTransactionReader.GetPositions(userId, asOfDate)).Where(p => p.Value > 0m).ToList();
        if (held.Count == 0) return [];

        var detailsById = await bondDashboardContext.LoadReferencedDetailsAsync(held.Select(p => p.PartitionId));
        List<UnrealizedGainLossInstrumentResult> results = [];
        foreach (var position in held)
        {
            detailsById.TryGetValue(position.PartitionId, out var details);

            var instrumentResult = await bondUnrealizedGainLossCalculator.CalculateAsync(position, details, currency, asOfDate);
            if (instrumentResult is not null)
                results.Add(instrumentResult);
        }

        return results;
    }
}