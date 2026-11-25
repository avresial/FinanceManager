using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Cash.Repositories;

namespace FinanceManager.Application.MoneyFlow.SavingsRate;

public class SavingsRateService(
    ICashTransactionReader cashTransactionReader,
    IFinancialLabelsRepository financialLabelsRepository,
    ICurrencyRepository currencyRepository,
    ICurrencyExchangeService currencyExchangeService) : ISavingsRateService
{
    public async IAsyncEnumerable<Domain.MoneyFlow.Entities.SavingsRate> GetSavingsRate(int userId, Currency currency, DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels().ToListAsync();
        var salaryLabel = labels.Single(x => x.Name.Equals("Salary", StringComparison.OrdinalIgnoreCase));
        var currencies = await currencyRepository.GetCurrencies().ToDictionaryAsync(x => x.Id);
        decimal salary = 0;
        decimal spending = 0;
        var labelsById = labels.ToDictionary(label => label.Id);

        foreach (var entry in await cashTransactionReader.GetLabeledEntries(userId, start, end, endInclusive: true))
        {
            var entryLabels = entry.LabelIds.Select(id => labelsById.GetValueOrDefault(id)).OfType<FinancialLabel>().ToList();
            var isSalary = entry.LabelIds.Contains(salaryLabel.Id);
            var isSpending = entry.ValueChange < 0 && FinancialLabelClassificationCatalog.ResolveClassification(entryLabels) != FinancialLabelClassificationCatalog.InvestmentValue;
            if (!isSalary && !isSpending) continue;

            var entryCurrency = currencies[entry.CurrencyId];
            var amount = entry.ValueChange;
            if (!string.Equals(entryCurrency.ShortName, currency.ShortName, StringComparison.OrdinalIgnoreCase))
            {
                var exchangeRate = await currencyExchangeService.GetExchangeRateAsync(entryCurrency, currency, entry.PostingDate);
                var rate = exchangeRate ?? throw new InvalidOperationException($"No exchange rate from {entryCurrency.ShortName} to {currency.ShortName} on {entry.PostingDate:d}.");
                amount *= rate;
            }

            if (isSalary) salary += amount;
            if (isSpending) spending += Math.Abs(amount);
        }

        if (salary == 0 && spending == 0) yield break;

        yield return new Domain.MoneyFlow.Entities.SavingsRate { Start = start, End = end, Salary = salary, Spending = spending };
    }
}