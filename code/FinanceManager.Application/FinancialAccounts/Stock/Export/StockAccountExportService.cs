using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Exports;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;
using System.Runtime.CompilerServices;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Stock.Export;

public class StockAccountExportService(
    IAccountRepository<StockAccount> stockAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository,
    IStockPriceProvider stockPriceProvider,
    IStockPriceRepository stockPriceRepository) : IStockAccountExportService
{
    public async IAsyncEnumerable<StockAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");

        await foreach (var entry in stockAccountEntryRepository.Get(accountId, start, end)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .WithCancellation(cancellationToken))
        {
            var stockCurrency = await stockPriceRepository.GetStockCurrency(entry.Isin);
            var price = stockCurrency is not null
                ? entry.Value * await stockPriceProvider.GetPricePerUnitAsync(entry.Isin, stockCurrency, entry.PostingDate)
                : 0m;

            yield return new StockAccountExportDto(entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Value, price, entry.Isin, entry.InvestmentType);
        }
    }
}