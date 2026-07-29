using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.MoneyFlow.InvestmentRate;

public class InvestmentRateService(
    IFinancialAccountRepository financialAccountRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    ICurrencyRepository currencyRepository,
    ICurrencyExchangeService currencyExchangeService) : IInvestmentRateService
{
    public async IAsyncEnumerable<Domain.MoneyFlow.Entities.InvestmentRate> GetInvestmentRate(int userId, Currency currency, DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels().ToListAsync();
        var salaryLabel = labels.Single(x => x.Name.ToLower() == "salary");

        // Only salary that actually landed inside the window counts. Borrowing an earlier month's
        // salary would report a rate against income the user has not received yet.
        decimal salary = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
            salary += SumSalary(account, salaryLabel);

        decimal investmentsChange = 0;
        var transactions = await investmentTransactionRepository.GetByUser(userId, DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));
        foreach (var transaction in transactions)
        {
            var amount = transaction.Type == Domain.FinancialAccounts.Investments.Entities.InvestmentTransactionType.Buy
                ? transaction.Quantity * transaction.UnitPrice + (transaction.Fee ?? 0m)
                : -transaction.Quantity * transaction.UnitPrice + (transaction.Fee ?? 0m);

            if (!string.Equals(transaction.Currency, currency.ShortName, StringComparison.OrdinalIgnoreCase))
            {
                var sourceCurrency = await currencyRepository.GetOrAdd(transaction.Currency, transaction.Currency);
                var exchangeRate = await currencyExchangeService.GetExchangeRateAsync(
                    sourceCurrency, currency, transaction.TradeDate.ToDateTime(TimeOnly.MinValue));
                amount *= exchangeRate ?? throw new InvalidOperationException($"No exchange rate from {sourceCurrency.ShortName} to {currency.ShortName} on {transaction.TradeDate}.");
            }

            investmentsChange += amount;
        }

        if (salary == 0 && investmentsChange == 0) yield break;

        yield return new()
        {
            Start = start,
            End = end,
            Salary = salary,
            InvestmentsChange = investmentsChange
        };
    }

    private static decimal SumSalary(CurrencyAccount account, FinancialLabel salaryLabel) =>
        GetSalaryEntries(account, salaryLabel).Sum(x => x.ValueChange);

    private static IEnumerable<CurrencyAccountEntry> GetSalaryEntries(CurrencyAccount account, FinancialLabel salaryLabel) =>
        account.Entries.Where(x => x.Labels is not null && x.Labels.Any(y => y.Id == salaryLabel.Id));
}