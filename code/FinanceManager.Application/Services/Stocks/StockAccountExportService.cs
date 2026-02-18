using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using System.Runtime.CompilerServices;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.Services.Stocks;

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
            var tickerCurrency = await stockPriceRepository.GetTickerCurrency(entry.Ticker);
            var price = tickerCurrency is not null
                ? entry.Value * await stockPriceProvider.GetPricePerUnitAsync(entry.Ticker, tickerCurrency, entry.PostingDate)
                : 0m;

            yield return new StockAccountExportDto(entry.PostingDate, entry.ValueChange, entry.Value, price, entry.Ticker, entry.InvestmentType);
        }
    }
}