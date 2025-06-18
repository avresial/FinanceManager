using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories
{
    public class AccountRepository : IFinancalAccountRepository
    {
        private readonly Random _random = new();
        private readonly IBankAccountRepository<BankAccount> _bankAccountAccountRepository;
        private readonly IAccountEntryRepository<BankAccountEntry> _bankAccountEntryRepository;
        private readonly IAccountRepository<StockAccount> _stockAccountRepository;
        private readonly IStockAccountEntryRepository<StockAccountEntry> _stockEntryRepository;

        public AccountRepository(IBankAccountRepository<BankAccount> bankAccountAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
            IAccountRepository<StockAccount> stockAccountRepository, IStockAccountEntryRepository<StockAccountEntry> _stockEntryRepository
            )
        {
            _bankAccountAccountRepository = bankAccountAccountRepository;
            _bankAccountEntryRepository = bankAccountEntryRepository;
            _stockAccountRepository = stockAccountRepository;
            this._stockEntryRepository = _stockEntryRepository;
        }

        public async Task<Dictionary<int, Type>> GetAvailableAccounts(int userId)
        {
            return (await _bankAccountAccountRepository.GetAvailableAccounts(userId))
                .ToDictionary(x => x.AccountId, x => typeof(BankAccount));
        }
        public async Task<int> GetLastAccountId()
        {
            throw new NotImplementedException();
        }
        public async Task<int> GetAccountsCount()
        {
            int bankAccountsCount = await _bankAccountAccountRepository.GetAccountsCount();
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

        public async Task<bool> AccountExists(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<T?> GetAccount<T>(int userId, int accountId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
        {
            if (typeof(T) == typeof(BankAccount))
            {
                var availableAccounts = (await _bankAccountAccountRepository.GetAvailableAccounts(userId)).Where(x => x.AccountId == accountId);

                foreach (var item in availableAccounts)
                {
                    var resultAccount = await _bankAccountAccountRepository.Get(item.AccountId);
                    if (resultAccount is null) continue;

                    IEnumerable<BankAccountEntry> entries = (await _bankAccountEntryRepository.Get(item.AccountId, dateStart, dateEnd)).ToList();

                    var nextOlderEntry = await _bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);
                    var nextYoungerEntry = await _bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);

                    var newResultAccount = new BankAccount(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                        resultAccount.AccountType, nextOlderEntry, nextYoungerEntry);

                    resultAccount.Add(entries, false);

                    if (newResultAccount is T resultElement) return resultElement;
                }
            }

            return null;
        }
        public async Task<T?> GetAccount<T>(int userId, int id) where T : BasicAccountInformation
        {
            return await GetAccount<T>(userId, id, DateTime.UtcNow, DateTime.UtcNow);
        }
        public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
        {
            List<T> result = new();
            if (typeof(T) == typeof(BankAccount))
            {
                var availableAccounts = await _bankAccountAccountRepository.GetAvailableAccounts(userId);

                foreach (var item in availableAccounts)
                {
                    var resultAccount = await _bankAccountAccountRepository.Get(item.AccountId);
                    if (resultAccount is null) continue;

                    IEnumerable<BankAccountEntry> entries = (await _bankAccountEntryRepository.Get(item.AccountId, dateStart, dateEnd)).ToList();

                    var nextOlderEntry = await _bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);

                    var nextYoungerEntry = await _bankAccountEntryRepository.GetNextYounger(item.AccountId, dateStart);

                    var newResultAccount = new BankAccount(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                        resultAccount.AccountType, nextOlderEntry, nextYoungerEntry);

                    resultAccount.Add(entries, false);
                    if (newResultAccount is T resultElement)
                        result.Add(resultElement);
                }
            }

            return result;
        }

        public async Task<int?> AddAccount<T>(T account) where T : BasicAccountInformation
        {
            switch (account)
            {
                case BankAccount bankAccount:
                    var bankAccountId = await _bankAccountAccountRepository.Add(bankAccount.UserId, 0, bankAccount.Name, bankAccount.AccountType);

                    if (bankAccount is not null && bankAccount.Entries is not null)
                        foreach (var entry in bankAccount.Entries)
                        {
                            entry.AccountId = bankAccountId ?? 0; // Ensure the entry has the correct account ID
                            await _bankAccountEntryRepository.Add(entry);
                        }

                    return bankAccountId;

                case StockAccount stockAccount:

                    var stockAccountId = await _stockAccountRepository.Add(account.UserId, 0, account.Name);

                    if (stockAccount is not null && stockAccount.Entries is not null)
                        foreach (var entry in stockAccount.Entries)
                        {
                            entry.AccountId = stockAccountId ?? 0; // Ensure the entry has the correct account ID
                            await _stockEntryRepository.Add(entry);
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

        public async Task<T?> GetNextYounger<T>(int accountId, DateTime date) where T : FinancialEntryBase => await _bankAccountEntryRepository.GetNextYounger(accountId, date) as T;
        public async Task AddEntry<T>(T bankAccountEntry, int id) where T : FinancialEntryBase
        {
            if (bankAccountEntry is BankAccountEntry bankEntry)
                await AddBankAccountEntry(id, bankEntry.ValueChange, bankEntry.Description, bankEntry.ExpenseType, bankEntry.PostingDate);
            if (bankAccountEntry is StockAccountEntry investmentEntry)
                await AddStockAccountEntry(id, investmentEntry.Ticker, investmentEntry.InvestmentType, investmentEntry.ValueChange, investmentEntry.PostingDate);
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
        public async Task Clear()
        {
            throw new NotImplementedException();
        }
        private async Task<object?> FindAccount(int id)
        {
            throw new NotImplementedException();
        }
        private async Task<T?> FindAccount<T>(int id) where T : BasicAccountInformation
        {
            throw new NotImplementedException();
        }


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
            ExpenseType expenseType = GetRandomType();
            if (accountType == AccountLabel.Stock)
                expenseType = ExpenseType.Investment;

            await AddAccount(new BankAccount(userId, accountId, accountName, accountType));
            await AddBankAccountEntry(accountId, startingBalance, $"Lorem ipsum {0}", expenseType, startDay);
            startDay = startDay.AddMinutes(1);
            int index = 0;
            while (startDay.Date <= DateTime.UtcNow.Date)
            {
                decimal balanceChange = (decimal)(_random.Next(-150, 200) + Math.Round(_random.NextDouble(), 2));

                expenseType = GetRandomType();
                if (accountType == AccountLabel.Stock)
                    expenseType = ExpenseType.Investment;

                await AddBankAccountEntry(accountId, balanceChange, $"Lorem ipsum {index++}", expenseType, startDay);
                startDay = startDay.AddDays(1);
            }
        }
        private async Task AddLoanAccount(int userId, DateTime startDay, decimal startingBalance, string accountName)
        {
            int accountId = (await GetLastAccountId()) + 1;

            await AddAccount(new BankAccount(userId, accountId, accountName, AccountLabel.Loan));

            await AddBankAccountEntry(accountId, startingBalance, $"Lorem ipsum {0}", ExpenseType.DebtRepayment, startDay);
            startDay = startDay.AddMinutes(1);
            decimal repaidAmount = 0;

            int index = 0;
            while (repaidAmount < -startingBalance && startDay.Date <= DateTime.Now.Date)
            {
                decimal balanceChange = (decimal)(_random.Next(0, 300) + Math.Round(_random.NextDouble(), 2));
                repaidAmount += balanceChange;
                if (repaidAmount >= -startingBalance)
                    balanceChange = repaidAmount + startingBalance;

                await AddBankAccountEntry(accountId, balanceChange, $"Lorem ipsum {index++}", ExpenseType.Other, startDay);

                startDay = startDay.AddDays(1);
            }
        }
        private async Task AddBankAccountEntry(int id, decimal balanceChange, string description, ExpenseType expenseType, DateTime? postingDate = null)
        {
            var account = await FindAccount<BankAccount>(id);
            if (account is null) return;

            var finalPostingDate = postingDate ?? DateTime.UtcNow;

            account.AddEntry(new AddBankEntryDto(finalPostingDate, balanceChange, expenseType, description));
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
        private ExpenseType GetRandomType()
        {
            Array values = Enum.GetValues<ExpenseType>();
            var result = values.GetValue(_random.Next(values.Length));
            if (result is null)
                return ExpenseType.Other;
            return (ExpenseType)result;
        }


    }
}
