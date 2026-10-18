using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.MoneyFlow.SavingsRate;

public class SavingsRateService(
    IFinancialAccountRepository financialAccountRepository,
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

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            var accountCurrency = currencies[account.CurrencyId];
            foreach (var entry in account.Entries.Where(entry => entry.PostingDate >= start && entry.PostingDate <= end))
            {
                var isSalary = entry.Labels?.Any(label => label.Id == salaryLabel.Id) == true;
                var isSpending = entry.ValueChange < 0 && FinancialLabelClassificationCatalog.ResolveClassification(entry.Labels ?? []) != FinancialLabelClassificationCatalog.InvestmentValue;
                if (!isSalary && !isSpending) continue;

                var amount = entry.ValueChange;
                if (!string.Equals(accountCurrency.ShortName, currency.ShortName, StringComparison.OrdinalIgnoreCase))
                {
                    var exchangeRate = await currencyExchangeService.GetExchangeRateAsync(accountCurrency, currency, entry.PostingDate);
                    var rate = exchangeRate ?? throw new InvalidOperationException($"No exchange rate from {accountCurrency.ShortName} to {currency.ShortName} on {entry.PostingDate:d}.");
                    amount *= rate;
                }

                if (isSalary) salary += amount;
                if (isSpending) spending += Math.Abs(amount);
            }
        }

        if (salary == 0 && spending == 0) yield break;

        yield return new Domain.MoneyFlow.Entities.SavingsRate { Start = start, End = end, Salary = salary, Spending = spending };
    }
}