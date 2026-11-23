using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Assets;

/// <summary>
/// Cash assets are the pooled cash balances that are positive: all of a user's cash entries in one currency net against each other,
/// whatever account they belong to, and a currency whose net balance is above zero is an asset in that currency (FX is not converted).
/// </summary>
internal class AssetsServiceCurrency(ICashTransactionReader cashTransactionReader, ICurrencyRepository currencyRepository) : IAssetsServiceTyped
{
    private const string _cashTypeName = "Cash";

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        if (start == new DateTime()) return [];

        var walk = new OwnerLedgerWalk(await cashTransactionReader.GetValueRange(userId, start, end));
        if (walk.IsEmpty) return [];

        // Every day carries an explicit value so a currency that turns negative does not keep yesterday's asset value.
        return [.. walk.EachDayEnd(start, end).Select(day => new TimeSeriesModel(day.Day, day.Positions.Where(p => p.Value > 0m).Sum(p => p.Value)))];
    }

    public Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType) =>
        investmentType == InvestmentType.Cash ? GetAssetsTimeSeries(userId, currency, start, end) : Task.FromResult(new List<TimeSeriesModel>());

    public async Task<AssetsDistributionResult> GetEndAssetsDistribution(
        int userId, Currency currency, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var assets = (await cashTransactionReader.GetPositions(userId, asOfDate, cancellationToken)).Where(p => p.Value > 0m).ToList();
        if (assets.Count == 0) return new([], []);

        var names = (await currencyRepository.GetCurrencies(cancellationToken).ToListAsync(cancellationToken)).ToDictionary(c => c.Id, c => c.ShortName);
        return new(GetPerType(assets).ToList(), GetPerCurrency(assets, names).ToList());
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
        (await cashTransactionReader.GetPositions(userId, DateTime.UtcNow)).Any(p => p.Value > 0m);

    public Task<List<UnrealizedGainLossAccountResult>> GetUnrealizedGainLossPerAccount(int userId, Currency currency, DateTime asOfDate) =>
        Task.FromResult(new List<UnrealizedGainLossAccountResult>());

    public Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate) =>
        Task.FromResult(new List<UnrealizedGainLossInstrumentResult>());

    private static IEnumerable<NameValueResult> GetPerType(IReadOnlyList<OwnerLedgerPosition> assets)
    {
        yield return new NameValueResult(_cashTypeName, assets.Sum(p => p.Value));
    }

    private static IEnumerable<NameValueResult> GetPerCurrency(IReadOnlyList<OwnerLedgerPosition> assets, Dictionary<int, string> names) =>
        assets.Select(p => new NameValueResult(names.GetValueOrDefault(p.PartitionId) ?? $"Currency {p.PartitionId}", p.Value)
        {
            Kind = _cashTypeName,
            CurrencyCode = names.GetValueOrDefault(p.PartitionId),
        });
}