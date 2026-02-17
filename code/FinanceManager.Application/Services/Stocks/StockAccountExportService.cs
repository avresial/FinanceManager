using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories.Account;
using System.Runtime.CompilerServices;
using UserId = int;
using AccountId = int;

namespace FinanceManager.Application.Services.Stocks;

public class StockAccountExportService(IAccountRepository<StockAccount> stockAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository) : IStockAccountExportService
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
            yield return new StockAccountExportDto(entry.PostingDate, entry.ValueChange, entry.Ticker, entry.InvestmentType);
        }
    }
}