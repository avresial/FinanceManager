using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

namespace FinanceManager.Application.Dashboard;

/// <summary>
/// Composes the dashboard transaction log by pulling the newest entries from every
/// account of every type (currency, bond, investment) and interleaving them into a
/// single newest-first list. The underlying repositories share a scoped EF Core
/// DbContext, which allows only one active query at a time — so each account stream
/// is fully buffered before the bounded entry queries run, and everything is awaited
/// sequentially.
/// </summary>
public class TransactionLogService(
    ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountRepository<BondAccount> bondAccountRepository,
    IAccountRepository<InvestmentAccount> investmentAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository,
    IAccountEntryRepository<BondAccountEntry> bondEntryRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    IBondDetailsRepository bondDetailsRepository) : ITransactionLogService
{
    private readonly Dictionary<int, string?> _bondNamesCache = [];

    public async Task<List<TransactionLogEntryDto>> GetLastTransactions(int userId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        List<TransactionLogEntryDto> result = [];

        var currencyAccounts = await BufferAccounts(currencyAccountRepository.GetAvailableAccounts(userId), cancellationToken);
        if (currencyAccounts.Count != 0)
        {
            var currencyAccountNames = currencyAccounts.ToDictionary(a => a.AccountId, a => a.AccountName);
            var entries = await currencyEntryRepository.GetMostRecentByAccounts(
                [.. currencyAccountNames.Keys], count, cancellationToken);

            result.AddRange(entries.Select(e => new TransactionLogEntryDto(
                e.AccountId,
                currencyAccountNames[e.AccountId],
                AccountType.Currency,
                e.EntryId,
                e.PostingDate,
                e.ValueChange,
                GetCurrencyDescription(e))));
        }

        var bondAccounts = await BufferAccounts(bondAccountRepository.GetAvailableAccounts(userId), cancellationToken);
        if (bondAccounts.Count != 0)
        {
            var bondAccountNames = bondAccounts.ToDictionary(a => a.AccountId, a => a.AccountName);
            var entries = await bondEntryRepository.GetMostRecentByAccounts(
                [.. bondAccountNames.Keys], count, cancellationToken);
            var bondNames = await GetBondNames(entries, cancellationToken);

            result.AddRange(entries.Select(e => new TransactionLogEntryDto(
                e.AccountId,
                bondAccountNames[e.AccountId],
                AccountType.Bond,
                e.EntryId,
                e.PostingDate,
                e.ValueChange,
                bondNames.TryGetValue(e.BondDetailsId, out var name) && name is not null ? name : "Bond")));
        }

        Dictionary<int, string> investmentAccounts = [];
        await foreach (var account in investmentAccountRepository.GetAvailableAccounts(userId).WithCancellation(cancellationToken))
            investmentAccounts[account.AccountId] = account.AccountName;

        if (investmentAccounts.Count != 0)
        {
            var transactions = await investmentTransactionRepository.GetMostRecentByAccounts([.. investmentAccounts.Keys], count, cancellationToken);
            result.AddRange(transactions
                .Select(t => new TransactionLogEntryDto(
                    t.AccountId,
                    investmentAccounts[t.AccountId],
                    AccountType.Stock,
                    t.Id,
                    t.TradeDate.ToDateTime(TimeOnly.MinValue),
                    t.SignedQuantity * t.UnitPrice,
                    GetInvestmentDescription(t))));
        }

        return [.. result
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.EntryId)
            .Take(count)];
    }

    // The account query must finish streaming before any entry query starts: relational
    // providers reject a second command while the previous reader is still open.
    private static async Task<List<AvailableAccount>> BufferAccounts(IAsyncEnumerable<AvailableAccount> accounts, CancellationToken cancellationToken)
    {
        List<AvailableAccount> result = [];
        await foreach (var account in accounts.WithCancellation(cancellationToken))
            result.Add(account);
        return result;
    }

    private static string GetCurrencyDescription(CurrencyAccountEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Description) ? entry.Description : entry.ContractorDetails ?? string.Empty;

    private static string GetInvestmentDescription(InvestmentTransaction transaction) =>
        $"{transaction.Type} {transaction.Quantity:0.####} {transaction.AssetListing?.Ticker}".TrimEnd();

    private async Task<IReadOnlyDictionary<int, string?>> GetBondNames(
        IReadOnlyList<BondAccountEntry> entries,
        CancellationToken cancellationToken)
    {
        var missingIds = entries
            .Select(entry => entry.BondDetailsId)
            .Distinct()
            .Where(id => !_bondNamesCache.ContainsKey(id))
            .ToList();

        if (missingIds.Count != 0)
        {
            var names = await bondDetailsRepository.GetNamesByIdsAsync(missingIds, cancellationToken);
            foreach (var id in missingIds)
                _bondNamesCache[id] = names.TryGetValue(id, out var name) ? name : null;
        }

        return _bondNamesCache;
    }
}