﻿using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories;

public class AccountRepository(IBankAccountRepository<BankAccount> bankAccountAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
    IAccountRepository<StockAccount> stockAccountRepository, IStockAccountEntryRepository<StockAccountEntry> stockEntryRepository) : IFinancialAccountRepository
{
    public Task<Dictionary<int, Type>> GetAvailableAccounts(int userId) => bankAccountAccountRepository.GetAvailableAccounts(userId)
        .ToDictionaryAsync(x => x.AccountId, x => typeof(BankAccount))
        .AsTask();

    public async Task<int> GetLastAccountId() => throw new NotImplementedException();
    public async Task<int> GetAccountsCount()
    {
        int bankAccountsCount = await bankAccountAccountRepository.GetAccountsCount();
        int stockAccountsCount = 0; // Add method to get stock accounts count
        return bankAccountsCount + stockAccountsCount;
    }
    public async Task<DateTime?> GetStartDate(int id)
    {
        var account = await FindAccount(id);
        if (account is null) return null;

        return account switch
        {
            BankAccount bankAccount => bankAccount.Start,
            StockAccount investmentAccount => investmentAccount.Start,
            _ => null,
        };
    }
    public async Task<DateTime?> GetEndDate(int id)
    {
        var account = await FindAccount(id);
        if (account is null) return null;

        return account switch
        {
            BankAccount bankAccount => bankAccount.End,
            StockAccount investmentAccount => investmentAccount.End,
            _ => null,
        };
    }

    public async Task<bool> AccountExists(int id) => throw new NotImplementedException();

    public async Task<T?> GetAccount<T>(int userId, int accountId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        if (await bankAccountAccountRepository.GetAvailableAccounts(userId).SingleAsync(x => x.AccountId == accountId) is null)
            throw new Exception($"User {userId} does not have account {accountId}");

        switch (typeof(T))
        {
            case Type t when t == typeof(BankAccount):

                var resultAccount = await bankAccountAccountRepository.Get(accountId);
                if (resultAccount is null) return null;

                var entries = await bankAccountEntryRepository.Get(resultAccount.AccountId, dateStart, dateEnd).ToListAsync();

                var nextOlderEntry = await bankAccountEntryRepository.GetNextOlder(resultAccount.AccountId, dateStart);
                var nextYoungerEntry = await bankAccountEntryRepository.GetNextOlder(resultAccount.AccountId, dateStart);

                if (entries.Count == 0 && nextOlderEntry is not null)
                    entries = [nextOlderEntry];

                var newResultAccount = new BankAccount(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                    resultAccount.AccountType, nextOlderEntry, nextYoungerEntry);

                resultAccount.Add(entries, false);

                if (newResultAccount is T resultElement) return resultElement;

                break;

            case Type t when t == typeof(StockAccount):

                var stockAccount = await stockAccountRepository.Get(accountId);
                if (stockAccount is null) throw new ArgumentNullException();

                var stockEntries = await stockEntryRepository.Get(stockAccount.AccountId, dateStart, dateEnd).ToListAsync();
                var stockNextOlderEntry = await stockEntryRepository.GetNextOlder(stockAccount.AccountId, dateStart);
                var stockNextYoungerEntry = await stockEntryRepository.GetNextYounger(stockAccount.AccountId, dateStart);

                if (stockEntries.Count == 0 && stockNextOlderEntry is not null)
                    stockEntries = stockNextOlderEntry.Values.ToList();

                var newStockResultAccount = new StockAccount(stockAccount.UserId, stockAccount.AccountId, stockAccount.Name, stockEntries, stockNextOlderEntry, stockNextYoungerEntry);
                newStockResultAccount.Add(stockEntries, false);

                if (newStockResultAccount is T newStockResult) return newStockResult;
                break;
        }

        return null;
    }
    public Task<T?> GetAccount<T>(int userId, int id) where T : BasicAccountInformation => GetAccount<T>(userId, id, DateTime.UtcNow, DateTime.UtcNow);
    public async IAsyncEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        switch (typeof(T))
        {
            case Type t when t == typeof(BankAccount):
                await foreach (var item in bankAccountAccountRepository.GetAvailableAccounts(userId))
                {
                    var resultAccount = await bankAccountAccountRepository.Get(item.AccountId);
                    if (resultAccount is null) continue;

                    var entries = await bankAccountEntryRepository.Get(item.AccountId, dateStart, dateEnd).ToListAsync();

                    var nextOlderEntry = await bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);
                    var nextYoungerEntry = await bankAccountEntryRepository.GetNextYounger(item.AccountId, dateStart);

                    var newResultAccount = new BankAccount(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                        resultAccount.AccountType, nextOlderEntry, nextYoungerEntry);

                    resultAccount.Add(entries, false);
                    if (newResultAccount is T resultElement)
                        yield return resultElement;
                }
                break;

            case Type t when t == typeof(StockAccount):
                await foreach (var item in stockAccountRepository.GetAvailableAccounts(userId))
                {
                    var resultAccount = await stockAccountRepository.Get(item.AccountId);
                    if (resultAccount is null) continue;
                    var entries = await stockEntryRepository.Get(item.AccountId, dateStart, dateEnd).ToListAsync();
                    var nextOlderEntry = await stockEntryRepository.GetNextOlder(item.AccountId, dateStart);
                    var nextYoungerEntry = await stockEntryRepository.GetNextYounger(item.AccountId, dateStart);
                    var newResultAccount = new StockAccount(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries, nextOlderEntry, nextYoungerEntry);
                    resultAccount.Add(entries, false);
                    if (newResultAccount is T resultElement)
                        yield return resultElement;
                }
                break;
        }
    }

    public async Task<int?> AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case BankAccount bankAccount:
                var bankAccountId = await bankAccountAccountRepository.Add(bankAccount.UserId, bankAccount.Name, bankAccount.AccountType);

                if (bankAccount is not null && bankAccount.Entries is not null)
                    foreach (var entry in bankAccount.Entries)
                    {
                        entry.AccountId = bankAccountId ?? 0; // Ensure the entry has the correct account ID
                        await bankAccountEntryRepository.Add(entry);
                    }

                return bankAccountId;

            case StockAccount stockAccount:

                var stockAccountId = await stockAccountRepository.Add(account.UserId, account.Name);

                if (stockAccount is not null && stockAccount.Entries is not null)
                    foreach (var entry in stockAccount.Entries)
                    {
                        entry.AccountId = stockAccountId ?? 0; // Ensure the entry has the correct account ID
                        await stockEntryRepository.Add(entry);
                    }

                return stockAccountId;
        }

        throw new NotSupportedException($"Account type {account.GetType()} is not supported.");
    }
    public async Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        throw new NotImplementedException();
    }
    public async Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        throw new NotImplementedException();
    }
    public async Task RemoveAccount(int id)
    {
        throw new NotImplementedException();
    }

    public async Task<T?> GetNextYounger<T>(int accountId, DateTime date) where T : FinancialEntryBase => await bankAccountEntryRepository.GetNextYounger(accountId, date) as T;
    public async Task AddEntry<T>(T bankAccountEntry, int id) where T : FinancialEntryBase
    {
        if (bankAccountEntry is BankAccountEntry bankEntry)
            await AddBankAccountEntry(id, bankEntry.ValueChange, bankEntry.Description, bankEntry.PostingDate);
        if (bankAccountEntry is StockAccountEntry investmentEntry)
            await AddStockAccountEntry(id, investmentEntry.Ticker, investmentEntry.InvestmentType, investmentEntry.ValueChange, investmentEntry.PostingDate);
    }
    public Task<bool> AddLabel<T>(T accountEntry, int labelId) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankEntry)
            return bankAccountEntryRepository.AddLabel(bankEntry.EntryId, labelId);

        return Task.FromResult(false);
    }
    public async Task UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankEntry)
            await UpdateBankAccountEntry(id, bankEntry);
        if (accountEntry is StockAccountEntry investmentEntry)
            await UpdateStockAccountEntry(id, investmentEntry);
    }
    public async Task RemoveEntry(int accountEntryId, int id)
    {
        var account = await FindAccount(id);
        if (account is null) return;

        switch (account)
        {
            case BankAccount bankAccount:
                bankAccount.Remove(accountEntryId);
                break;
            case StockAccount investmentAccount:
                investmentAccount.Remove(accountEntryId);
                break;
        }
    }
    public async Task Clear() =>
        throw new NotImplementedException();
    private async Task<object?> FindAccount(int id) =>
        throw new NotImplementedException();
    private async Task<T?> FindAccount<T>(int id) where T : BasicAccountInformation =>
        throw new NotImplementedException();


    private async Task AddStockAccountEntry(int id, string ticker, InvestmentType investmentType, decimal balanceChange, DateTime? postingDate = null)
    {
        var account = await FindAccount<StockAccount>(id);
        if (account is null) return;

        var finalPostingDate = postingDate ?? DateTime.UtcNow;

        account.Add(new AddInvestmentEntryDto(finalPostingDate, balanceChange, ticker, investmentType));
    }
    private async Task AddBankAccount(int userId, DateTime startDay, decimal startingBalance, string accountName, AccountLabel accountType)
    {
        int accountId = (await GetLastAccountId()) + 1;

        await AddAccount(new BankAccount(userId, accountId, accountName, accountType));
        await AddBankAccountEntry(accountId, startingBalance, $"Lorem ipsum {0}", startDay);
        startDay = startDay.AddMinutes(1);
        int index = 0;
        while (startDay.Date <= DateTime.UtcNow.Date)
        {
            decimal balanceChange = (decimal)(Random.Shared.Next(-150, 200) + Math.Round(Random.Shared.NextDouble(), 2));


            await AddBankAccountEntry(accountId, balanceChange, $"Lorem ipsum {index++}", startDay);
            startDay = startDay.AddDays(1);
        }
    }
    private async Task AddLoanAccount(int userId, DateTime startDay, decimal startingBalance, string accountName)
    {
        int accountId = (await GetLastAccountId()) + 1;

        await AddAccount(new BankAccount(userId, accountId, accountName, AccountLabel.Loan));

        await AddBankAccountEntry(accountId, startingBalance, $"Lorem ipsum {0}", startDay);
        startDay = startDay.AddMinutes(1);
        decimal repaidAmount = 0;

        int index = 0;
        while (repaidAmount < -startingBalance && startDay.Date <= DateTime.Now.Date)
        {
            decimal balanceChange = (decimal)(Random.Shared.Next(0, 300) + Math.Round(Random.Shared.NextDouble(), 2));
            repaidAmount += balanceChange;
            if (repaidAmount >= -startingBalance)
                balanceChange = repaidAmount + startingBalance;

            await AddBankAccountEntry(accountId, balanceChange, $"Lorem ipsum {index++}", startDay);

            startDay = startDay.AddDays(1);
        }
    }
    private async Task AddBankAccountEntry(int id, decimal balanceChange, string description, DateTime? postingDate = null)
    {
        var account = await FindAccount<BankAccount>(id);
        if (account is null) return;

        var finalPostingDate = postingDate ?? DateTime.UtcNow;

        account.AddEntry(new AddBankEntryDto(finalPostingDate, balanceChange, description, [new() { Name = "Sallary" }]));
    }
    private async Task UpdateBankAccountEntry(int id, BankAccountEntry bankAccountEntry)
    {
        var bankAccount = await FindAccount<BankAccount>(id);
        if (bankAccount is null || bankAccount.Entries is null) return;

        var entryToUpdate = bankAccount.Entries.FirstOrDefault(x => x.EntryId == bankAccountEntry.EntryId);
        if (entryToUpdate is null) return;

        entryToUpdate.Update(bankAccountEntry);
    }
    private async Task UpdateStockAccountEntry(int id, StockAccountEntry investmentEntry)
    {
        var investmentAccount = await FindAccount<StockAccount>(id);
        if (investmentAccount is null || investmentAccount.Entries is null) return;

        var entryToUpdate = investmentAccount.Entries.FirstOrDefault(x => x.EntryId == investmentEntry.EntryId);
        if (entryToUpdate is null) return;

        entryToUpdate.Update(investmentEntry);
    }
}