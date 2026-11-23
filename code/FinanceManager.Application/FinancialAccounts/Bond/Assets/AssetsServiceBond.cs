using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Extensions;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Bond.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
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
/// and interest accrues from the latest pooled entry. Unrealized gain/loss still reads account entries until the portfolio step.
/// </summary>
public class AssetsServiceBond(
    IBondTransactionReader bondTransactionReader,
    BondDashboardContext bondDashboardContext,
    IFinancialAccountRepository financialAccountRepository,
    IBondDetailsRepository bondDetailsRepository,
    IBondUnrealizedGainLossCalculator bondUnrealizedGainLossCalculator) : IAssetsServiceTyped
{
    private const string _bondTypeName = "Bond";

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
        var instrumentResults = await GetUnrealizedGainLossPerInstrument(userId, currency, asOfDate);
        var byAccount = instrumentResults.GroupBy(x => new { x.AccountId, x.AccountName });

        List<UnrealizedGainLossAccountResult> results = [];
        foreach (var accountGroup in byAccount)
        {
            var included = accountGroup.Where(x => !x.IsExcludedFromTotals).ToList();
            var excludedCount = accountGroup.Count(x => x.IsExcludedFromTotals);

            var costBasis = included.Sum(x => x.CostBasis);
            var currentValue = included.Sum(x => x.CurrentValue);
            var gainLoss = new GainLoss(costBasis, currentValue);

            results.Add(new UnrealizedGainLossAccountResult(
                accountGroup.Key.AccountId,
                accountGroup.Key.AccountName,
                costBasis,
                currentValue,
                gainLoss.Amount,
                gainLoss.Percent,
                asOfDate,
                excludedCount
            ));
        }

        return results;
    }

    public async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate)
    {
        var bondDetailsById = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);
        List<UnrealizedGainLossInstrumentResult> results = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, DateTime.MinValue, asOfDate).Where(x => x.ContainsAssets))
        {
            foreach (var bondDetailsId in account.GetStoredBondsIds())
            {
                bondDetailsById.TryGetValue(bondDetailsId, out var details);

                var instrumentResult = await bondUnrealizedGainLossCalculator.CalculateAsync(account, bondDetailsId, details, currency, asOfDate);
                if (instrumentResult is not null)
                    results.Add(instrumentResult);
            }
        }

        return results;
    }
}