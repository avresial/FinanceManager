using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;
public class MoneyFlowService(IFinancialAccountRepository financialAccountRepository, IFinancialLabelsRepository financialLabelsRepository, IStockPriceProvider stockPriceProvider) : IMoneyFlowService
{
    public async Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date)
    {
        if (date > DateTime.UtcNow) date = DateTime.UtcNow;
        decimal result = 0;

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, date.Date, date))
        {
            var newestEntry = account.GetThisOrNextOlder(date);
            if (newestEntry is null) continue;

            result += newestEntry.Value;
        }

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, date.Date, date))
        {
            foreach (var ticker in account.GetStoredTickers())
            {
                var newestEntry = account.GetThisOrNextOlder(date, ticker);
                if (newestEntry is null) continue;

                decimal pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, date);
                result += newestEntry.Value * pricePerUnit;
            }
        }

        return Math.Round(result, 2);
    }
    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (start == new DateTime()) return [];
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];

        for (DateTime date = end; date >= start; date = date.AddDays(-1))
        {
            var netWorth = await GetNetWorth(userId, currency, date);
            if (netWorth is null) continue;
            result.Add(date, netWorth.Value);
        }
        return result;
    }
    public async Task<List<TimeSeriesModel>> GetIncome(int userId, Currency currency, DateTime start, DateTime end)
    {
        TimeSpan timeSeriesStep = new(1, 0, 0, 0);

        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            for (var date = end; date >= start; date = date.Add(-timeSeriesStep))
            {
                if (!result.ContainsKey(date)) result.Add(date, 0);

                if (account.Entries is null) continue;
                var entries = account.Get(date);

                foreach (var entry in entries.Where(x => x.ValueChange > 0 && x.PostingDate.Date == date.Date).Select(x => x as FinancialEntryBase))
                    result[date] += entry.ValueChange;
            }
        }

        return TimeBucketService.Get(result.Select(x => (x.Key, x.Value))).Select(x => new TimeSeriesModel(x.Date, x.Objects.Sum(x => x))).ToList();
    }
    public async Task<List<TimeSeriesModel>> GetSpending(int userId, Currency currency, DateTime start, DateTime end)
    {
        TimeSpan timeSeriesStep = new(1, 0, 0, 0);
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];
        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            for (var date = end; date >= start; date = date.Add(-timeSeriesStep))
            {
                if (!result.ContainsKey(date)) result.Add(date, 0);

                if (account.Entries is null) continue;
                var entries = account.Get(date);

                foreach (var entry in entries.Where(x => x.ValueChange < 0 && x.PostingDate.Date == date.Date).Select(x => x as FinancialEntryBase))
                    result[date] += entry.ValueChange;
            }
        }

        return TimeBucketService.Get(result.Select(x => (x.Key, x.Value)))
            .Select(x => new TimeSeriesModel(x.Date, x.Objects.Sum(x => x)))
            .ToList();
    }
    public Task<List<TimeSeriesModel>> GetBalance(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        throw new NotImplementedException();
    }
    public async Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        var labels = await financialLabelsRepository.GetLabels().ToListAsync();

        var result = labels.ToDictionary(x => x.Id, x => new NameValueResult() { Name = x.Name, Value = 0 });
        await foreach (BankAccount account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) continue;
            if (account.Entries is null || !account.Entries.Any()) continue;

            foreach (var entry in account.Entries.Where(x => x.Labels is not null && x.Labels.Any()))
            {
                foreach (var label in entry.Labels)
                {
                    if (!result.ContainsKey(label.Id)) continue;
                    result[label.Id].Value += entry.ValueChange;
                }
            }
        }

        // TODO: Add labels for stock accounts?

        return result.Values.ToList();
    }
    public async IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels().ToListAsync();
        var salaryLabel = labels.Single(x => x.Name.ToLower() == "salary");

        Currency currency = DefaultCurrency.PLN; // TODO: use user currency settings

        decimal salary = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
            salary += account.Entries.Where(x => x.Labels is not null && x.Labels.Any(y => y.Id == salaryLabel.Id)).Sum(x => x.ValueChange);

        if (salary == 0) yield break;

        decimal investmentsChange = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end))
            foreach (var entry in account.Entries)
                investmentsChange += entry.ValueChange * await stockPriceProvider.GetPricePerUnitAsync(entry.Ticker, currency, entry.PostingDate);

        yield return new()
        {
            Start = start,
            End = end,
            Salary = salary,
            InvestmentsChange = investmentsChange
        };
    }
}