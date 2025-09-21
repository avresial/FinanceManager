using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;
public class MoneyFlowService(IFinancialAccountRepository financialAccountRepository, IStockPriceRepository stockRepository,
    ICurrencyExchangeService currencyExchangeService, IFinancialLabelsRepository financialLabelsRepository) : IMoneyFlowService
{

    public async Task<decimal?> GetNetWorth(int userId, string currency, DateTime date)
    {
        if (date > DateTime.UtcNow) date = DateTime.UtcNow;
        decimal result = 0;

        var bankAccounts = await financialAccountRepository.GetAccounts<BankAccount>(userId, date.Date, date).ToListAsync();
        foreach (var bankAccount in bankAccounts)
        {
            if (bankAccount.NextOlderEntry is null) continue;
            if (bankAccount.Entries is null) continue;

            var newBankAccount = await financialAccountRepository.GetAccount<BankAccount>(userId, bankAccount.AccountId, bankAccount.NextOlderEntry.PostingDate,
                bankAccount.NextOlderEntry.PostingDate.AddSeconds(1));
            if (newBankAccount is not null && newBankAccount.Entries is not null)
                bankAccount.Add(newBankAccount.Entries, false);
        }

        foreach (var account in bankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0))
        {
            if (account is null || account.Entries is null) continue;

            var newestEntry = account.Get(date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
            if (newestEntry is null) continue;

            result += newestEntry.Value;
        }

        var investmentAccounts = await financialAccountRepository.GetAccounts<StockAccount>(userId, date.Date, date).ToListAsync();
        foreach (var investmentAccount in investmentAccounts)
        {
            foreach (var item in investmentAccount.NextOlderEntries)
            {
                if (investmentAccount.Entries is null) continue;
                if (investmentAccount.Entries.Any(x => x.Ticker == item.Key)) continue;

                var newInvestmentAccount = await financialAccountRepository.GetAccount<StockAccount>(userId, investmentAccount.AccountId, item.Value.PostingDate, item.Value.PostingDate.AddSeconds(1));
                if (newInvestmentAccount is not null && newInvestmentAccount.Entries is not null)
                    investmentAccount.Add(newInvestmentAccount.Entries, false);
            }
        }

        foreach (var account in investmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0))
        {
            if (account is null || account.Entries is null) continue;

            foreach (var tickerGroup in account.Get(date).GroupBy(x => x.Ticker))
            {
                var newestEntry = tickerGroup.OrderByDescending(x => x.PostingDate).FirstOrDefault();
                if (newestEntry is null) continue;
                var stockPrice = await stockRepository.GetThisOrNextOlder(newestEntry.Ticker, date);
                decimal pricePerUnit = stockPrice is null ? 1 : await currencyExchangeService.GetPricePerUnit(stockPrice, currency, date);
                result += newestEntry.Value * pricePerUnit;
            }
        }

        return Math.Round(result, 2);
    }
    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, string currency, DateTime start, DateTime end)
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
    public async Task<List<TimeSeriesModel>> GetIncome(int userId, string currency, DateTime start, DateTime end)
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
        var timeBucket = TimeBucketService.Get(result.Select(x => (x.Key, x.Value)));
        return timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }).ToList();
    }
    public async Task<List<TimeSeriesModel>> GetSpending(int userId, string currency, DateTime start, DateTime end)
    {
        TimeSpan timeSeriesStep = new(1, 0, 0, 0);
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];
        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            for (var date = end; date >= start; date = date.Add(-timeSeriesStep)) // TODO fix for time series step other than 1 day
            {
                if (!result.ContainsKey(date)) result.Add(date, 0);

                if (account.Entries is null) continue;
                var entries = account.Get(date);

                foreach (var entry in entries.Where(x => x.ValueChange < 0 && x.PostingDate.Date == date.Date).Select(x => x as FinancialEntryBase))
                    result[date] += entry.ValueChange;
            }
        }

        var timeBucket = TimeBucketService.Get(result.Select(x => (x.Key, x.Value)));
        return timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }).ToList();
    }
    public Task<List<TimeSeriesModel>> GetBalance(int userId, string currency, DateTime start, DateTime end)
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

        decimal salary = 0;
        await foreach (BankAccount account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) continue;
            if (account.Entries is null || !account.Entries.Any()) continue;

            salary += account.Entries.Where(x => x.Labels is not null && x.Labels.Any(y => y.Id == salaryLabel.Id)).Sum(x => x.ValueChange);
        }
        if (salary == 0) yield break;

        decimal investmentsChange = 0;
        var investmentAccounts = financialAccountRepository.GetAccounts<StockAccount>(userId, start, end);
        await foreach (StockAccount account in investmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0))
        {
            if (account is null || account.Entries is null) continue;

            foreach (var entry in account.Entries)
            {
                var stockPrice = await stockRepository.GetThisOrNextOlder(entry.Ticker, entry.PostingDate);
                decimal pricePerUnit = stockPrice is null ? 1 : await currencyExchangeService.GetPricePerUnit(stockPrice, "PLN", entry.PostingDate);
                investmentsChange += entry.ValueChange * pricePerUnit;
            }
        }

        yield return new()
        {
            Start = start,
            End = end,
            Salary = salary,
            InvestmentsChange = investmentsChange
        };
    }
}