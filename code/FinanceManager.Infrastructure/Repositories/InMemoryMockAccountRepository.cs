using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using System.ComponentModel.Design;

namespace FinanceManager.Infrastructure.Repositories
{
    public class InMemoryMockAccountRepository : IFinancalAccountRepository
    {
        private readonly Random _random = new();
        private ServiceContainer _bankAccounts = new();
        private readonly Dictionary<int, Type> _nameTypeDictionary = [];
        private readonly IAccountRepository<BankAccount> _bankAccountAccountRepository;
        private readonly IAccountEntryRepository<BankAccountEntry> _bankAccountEntryRepository;

        public InMemoryMockAccountRepository(IAccountRepository<BankAccount> bankAccountAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository)
        {
            _bankAccountAccountRepository = bankAccountAccountRepository;
            _bankAccountEntryRepository = bankAccountEntryRepository;
        }

        public Dictionary<int, Type> GetAvailableAccounts()
        {
            return _nameTypeDictionary;
        }
        public int GetLastAccountId()
        {
            if (_nameTypeDictionary.Keys is null || _nameTypeDictionary.Keys.Count == 0) return 0;
            return _nameTypeDictionary.Keys.Max();
        }
        public DateTime? GetStartDate(int id)
        {
            var account = FindAccount(id);
            if (account is null) return null;

            return account switch
            {
                BankAccount bankAccount => bankAccount.Start,
                StockAccount investmentAccount => investmentAccount.Start,
                _ => null,
            };
        }
        public DateTime? GetEndDate(int id)
        {
            var account = FindAccount(id);
            if (account is null) return null;

            return account switch
            {
                BankAccount bankAccount => bankAccount.End,
                StockAccount investmentAccount => investmentAccount.End,
                _ => null,
            };
        }

        public bool AccountExists(int id)
        {
            return _nameTypeDictionary.ContainsKey(id);
        }
        public T? GetAccount<T>(int userId, int accountId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
        {
            if (_bankAccounts.GetService(typeof(List<T>)) is not List<T> accountsOfType) return default;
            if (dateStart == new DateTime() || dateEnd == new DateTime()) return default;

            var firstAccount = accountsOfType.FirstOrDefault(x => x.AccountId == accountId);
            if (firstAccount is null) return default;

            if (typeof(T) == typeof(BankAccount))
            {
                if (firstAccount is not BankAccount databaseBankAccount) return default;
                DateTime? olderThenLoadedEntryDate = null;

                var olderThenLoadedEntry = databaseBankAccount.Get(dateStart.AddSeconds(-1)).FirstOrDefault();

                if (olderThenLoadedEntry is not null)
                    olderThenLoadedEntryDate = olderThenLoadedEntry.PostingDate;

                BankAccount account = new(userId, databaseBankAccount.AccountId, databaseBankAccount.Name, [], databaseBankAccount.AccountType, olderThenLoadedEntryDate);

                foreach (var element in databaseBankAccount.Get(dateStart, dateEnd))
                {
                    var testValue = element.GetCopy();
                    account.Add(testValue, false);
                }

                return account as T;
            }

            if (typeof(T) == typeof(StockAccount))
            {
                if (firstAccount is not StockAccount databaseInvestmentAccount) return default;
                Dictionary<string, DateTime> olderThenLoadedEntry = [];
                foreach (var entry in databaseInvestmentAccount.Get(dateStart.AddSeconds(-1)))
                {
                    if (!olderThenLoadedEntry.ContainsKey(entry.Ticker))
                        olderThenLoadedEntry.Add(entry.Ticker, entry.PostingDate);
                }
                StockAccount bankAccount = new(userId, databaseInvestmentAccount.AccountId, databaseInvestmentAccount.Name, [], olderThenLoadedEntry);

                foreach (var element in databaseInvestmentAccount.Get(dateStart, dateEnd))
                    bankAccount.Add(element.GetCopy(), false);

                return bankAccount as T;
            }

            return default;
        }
        public T? GetAccount<T>(int userId, int id) where T : BasicAccountInformation
        {
            return GetAccount<T>(userId, id, DateTime.UtcNow, DateTime.UtcNow);
        }
        public IEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
        {
            if (typeof(T) == typeof(BankAccount))
            {
                var availableAccounts = _bankAccountAccountRepository.GetAvailableAccounts(userId);

                foreach (var item in availableAccounts)
                {
                    var resultAccount = _bankAccountAccountRepository.Get(item.AccountId);
                    if (resultAccount is null) continue;

                    IEnumerable<BankAccountEntry> entries = _bankAccountEntryRepository.Get(item.AccountId, dateStart, dateEnd);

                    DateTime? olderThenLoadedEntryDate = null;
                    DateTime? youngerThenLoadedEntryDate = null;

                    if (entries.Any())
                    {
                        var olderEntry = _bankAccountEntryRepository.GetNextOlder(userId, entries.Last().EntryId);
                        if (olderEntry is not null) olderThenLoadedEntryDate = olderEntry.PostingDate;

                        var youngerEntry = _bankAccountEntryRepository.GetNextOlder(userId, entries.Last().EntryId);
                        if (youngerEntry is not null) youngerThenLoadedEntryDate = youngerEntry.PostingDate;
                    }

                    resultAccount.Add(entries, false);

                    yield return resultAccount as T;
                }
            }

        }

        public void AddAccount<T>(T bankAccount) where T : BasicAccountInformation
        {
            if (_bankAccounts is null) return;

            var accountsOfType = (List<T>?)_bankAccounts.GetService(typeof(List<T>));
            if (accountsOfType is null)
                _bankAccounts.AddService(typeof(List<T>), new List<T>() { bankAccount });
            else
                accountsOfType.Add(bankAccount);

            _nameTypeDictionary.Add(bankAccount.AccountId, typeof(T));
        }
        public void AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
            where AccountType : BasicAccountInformation
            where EntryType : FinancialEntryBase
        {
            throw new NotImplementedException();
        }
        public void UpdateAccount<T>(T account) where T : BasicAccountInformation
        {
            if (_bankAccounts is null) return;

            if (!AccountExists(account.AccountId)) throw new Exception("Account does not exist");

            switch (account)
            {
                case BankAccount bankAccount:
                    List<BankAccount>? bankAccounts = _bankAccounts.GetService(typeof(List<BankAccount>)) as List<BankAccount>;
                    if (bankAccounts is not null)
                    {
                        var accountToUpdate = bankAccounts.FirstOrDefault(x => x.AccountId == bankAccount.AccountId);
                        if (accountToUpdate is not null)
                        {
                            accountToUpdate.Name = bankAccount.Name;
                            accountToUpdate.AccountType = bankAccount.AccountType;

                        }
                    }
                    break;
                case StockAccount investmentAccount:
                    List<StockAccount>? investmentAccounts = _bankAccounts.GetService(typeof(List<StockAccount>)) as List<StockAccount>;
                    if (investmentAccounts is not null)
                    {
                        var accountToUpdate = investmentAccounts.FirstOrDefault(x => x.AccountId == investmentAccount.AccountId);
                        if (accountToUpdate is not null)
                            accountToUpdate.Name = investmentAccount.Name;
                    }
                    break;
                default:
                    throw new Exception($"Unexpected type: {account.GetType()} - account id:{account.AccountId}"); // make new exception
            }
        }
        public void RemoveAccount(int id)
        {
            if (_bankAccounts is null) return;

            if (!AccountExists(id)) throw new Exception("Account does not exist");

            var accountType = GetAvailableAccounts()[id];

            if (accountType == typeof(BankAccount))
            {
                List<BankAccount>? accountsOfType = _bankAccounts.GetService(typeof(List<BankAccount>)) as List<BankAccount>;
                accountsOfType?.RemoveAll(x => x.AccountId == id);
            }
            else if (accountType == typeof(StockAccount))
            {
                List<StockAccount>? accountsOfType = _bankAccounts.GetService(typeof(List<StockAccount>)) as List<StockAccount>;
                accountsOfType?.RemoveAll(x => x.AccountId == id);
            }
            else
            {
                throw new Exception($"Unexpected type: {accountType} - account id:{id}"); // make new exception
            }

            _nameTypeDictionary.Remove(id);
        }

        public void AddEntry<T>(T bankAccountEntry, int id) where T : FinancialEntryBase
        {
            if (bankAccountEntry is BankAccountEntry bankEntry)
                AddBankAccountEntry(id, bankEntry.ValueChange, bankEntry.Description, bankEntry.ExpenseType, bankEntry.PostingDate);
            if (bankAccountEntry is StockAccountEntry investmentEntry)
                AddStockAccountEntry(id, investmentEntry.Ticker, investmentEntry.InvestmentType, investmentEntry.ValueChange, investmentEntry.PostingDate);
        }
        public void UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
        {
            if (accountEntry is BankAccountEntry bankEntry)
                UpdateBankAccountEntry(id, bankEntry);
            if (accountEntry is StockAccountEntry investmentEntry)
                UpdateStockAccountEntry(id, investmentEntry);
        }
        public void RemoveEntry(int accountEntryId, int id)
        {
            var account = FindAccount(id);
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
        public void Clear()
        {
            _bankAccounts.Dispose();
            _bankAccounts = new();
            _nameTypeDictionary.Clear();
        }
        private object? FindAccount(int id)
        {
            if (_bankAccounts is null) return null;
            if (!_nameTypeDictionary.TryGetValue(id, out Type? accountType)) return null;
            if (accountType == typeof(BankAccount))
            {
                if (_bankAccounts.GetService(typeof(List<BankAccount>)) is not List<BankAccount> bankAccounts) return null;

                return bankAccounts.FirstOrDefault(x => x.AccountId == id);
            }

            if (accountType == typeof(StockAccount))
            {
                if (_bankAccounts.GetService(typeof(List<StockAccount>)) is not List<StockAccount> investmentAccounts) return null;

                return investmentAccounts.FirstOrDefault(x => x.AccountId == id);
            }

            return null;
        }
        private T? FindAccount<T>(int id) where T : BasicAccountInformation
        {
            if (_bankAccounts.GetService(typeof(List<T>)) is not List<T> accountsOfType) return default;

            var firstAccount = accountsOfType.FirstOrDefault(x => x.AccountId == id);
            if (firstAccount is null) return default;

            return firstAccount;
        }
        private void AddMockData()
        {
            AddBankAccount(1, DateTime.UtcNow.AddMonths(-8), 100, "Main", AccountType.Cash);
            AddBankAccount(1, DateTime.UtcNow.AddMinutes(-1), 0, "Empty Bank", AccountType.Cash);
            //		AddBankAccount(DateTime.UtcNow.AddMonths(-8), 10, "Cash", AccountType.Cash);

            //AddBankAccount(DateTime.UtcNow.AddMonths(-8), 10, "Bonds", AccountType.Bond);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-12), 10, "Nvidia", AccountType.Stock);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-12), 10, "Intel", AccountType.Stock);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-12), 10, "S&P 500", AccountType.Stock);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-2), 10, "PPK", AccountType.Stock);

            //AddBankAccount(DateTime.UtcNow.AddMonths(-3), 10000, "Apartment 1", AccountType.RealEstate);
            //AddLoanAccount(DateTime.UtcNow.AddMonths(-2), -300 * 62, "Loan 1");

            //AddStockAccount(DateTime.UtcNow.AddDays(-10), 10, "Wallet 1",
            //[
            //    ("S&P 500", InvestmentType.Stock)
            //    ,( "Orlen", InvestmentType.Stock), 
            //    //("Nvidia", InvestmentType.Stock),( "Intel", InvestmentType.Stock), ("Bonds" , InvestmentType.Bond)
            //]);

            //AddStockAccount(DateTime.UtcNow.AddDays(-10), 10, "Wallet 2",
            //[
            //    ("Nvidia", InvestmentType.Stock),( "Intel", InvestmentType.Stock), ("Bond 1" , InvestmentType.Bond)
            //]);

            //AddStockAccount(DateTime.UtcNow.AddMonths(-1), 10, "Wallet 3",
            //[
            //    ("Bond 2", InvestmentType.Bond),( "Bond 3", InvestmentType.Bond), ("Bond 4" , InvestmentType.Bond)
            //]);

            //AddStockAccount(DateTime.UtcNow.AddMonths(-1), 10, "Empty Wallet", []);
        }
        private void AddStockAccount(int userId, DateTime startDay, decimal startingBalance, string accountName, List<(string, InvestmentType)> tickers)
        {
            int accountId = GetLastAccountId() + 1;
            AddAccount(new StockAccount(userId, accountId, accountName));
            if (tickers is null || tickers.Count == 0) return;
            int tickerIndex = _random.Next(tickers.Count);
            AddStockAccountEntry(accountId, tickers[tickerIndex].Item1, tickers[tickerIndex].Item2, startingBalance, startDay);

            while (startDay.Date <= DateTime.UtcNow.Date)
            {
                tickerIndex = _random.Next(tickers.Count);
                decimal balanceChange = (decimal)(_random.Next(-150, 200) + Math.Round(_random.NextDouble(), 2));

                AddStockAccountEntry(accountId, tickers[tickerIndex].Item1, tickers[tickerIndex].Item2, balanceChange, startDay);
                startDay = startDay.AddDays(1);
            }
        }
        private void AddStockAccountEntry(int id, string ticker, InvestmentType investmentType, decimal balanceChange, DateTime? postingDate = null)
        {
            var account = FindAccount<StockAccount>(id);
            if (account is null) return;

            var finalPostingDate = postingDate ?? DateTime.UtcNow;

            account.Add(new AddInvestmentEntryDto(finalPostingDate, balanceChange, ticker, investmentType));
        }
        private void AddBankAccount(int userId, DateTime startDay, decimal startingBalance, string accountName, AccountType accountType)
        {
            int accountId = GetLastAccountId() + 1;
            ExpenseType expenseType = GetRandomType();
            if (accountType == AccountType.Stock)
                expenseType = ExpenseType.Investment;

            AddAccount(new BankAccount(userId, accountId, accountName, accountType));
            AddBankAccountEntry(accountId, startingBalance, $"Lorem ipsum {0}", expenseType, startDay);
            startDay = startDay.AddMinutes(1);
            int index = 0;
            while (startDay.Date <= DateTime.UtcNow.Date)
            {
                decimal balanceChange = (decimal)(_random.Next(-150, 200) + Math.Round(_random.NextDouble(), 2));

                expenseType = GetRandomType();
                if (accountType == AccountType.Stock)
                    expenseType = ExpenseType.Investment;

                AddBankAccountEntry(accountId, balanceChange, $"Lorem ipsum {index++}", expenseType, startDay);
                startDay = startDay.AddDays(1);
            }
        }
        private void AddLoanAccount(int userId, DateTime startDay, decimal startingBalance, string accountName)
        {
            int accountId = GetLastAccountId() + 1;

            AddAccount(new BankAccount(userId, accountId, accountName, AccountType.Loan));

            AddBankAccountEntry(accountId, startingBalance, $"Lorem ipsum {0}", ExpenseType.DebtRepayment, startDay);
            startDay = startDay.AddMinutes(1);
            decimal repaidAmount = 0;

            int index = 0;
            while (repaidAmount < -startingBalance && startDay.Date <= DateTime.Now.Date)
            {
                decimal balanceChange = (decimal)(_random.Next(0, 300) + Math.Round(_random.NextDouble(), 2));
                repaidAmount += balanceChange;
                if (repaidAmount >= -startingBalance)
                    balanceChange = repaidAmount + startingBalance;

                AddBankAccountEntry(accountId, balanceChange, $"Lorem ipsum {index++}", ExpenseType.Other, startDay);

                startDay = startDay.AddDays(1);
            }
        }
        private void AddBankAccountEntry(int id, decimal balanceChange, string description, ExpenseType expenseType, DateTime? postingDate = null)
        {
            var account = FindAccount<BankAccount>(id);
            if (account is null) return;

            var finalPostingDate = postingDate ?? DateTime.UtcNow;

            account.AddEntry(new AddBankEntryDto(finalPostingDate, balanceChange, expenseType, description));
        }
        private void UpdateBankAccountEntry(int id, BankAccountEntry bankAccountEntry)
        {
            var bankAccount = FindAccount<BankAccount>(id);
            if (bankAccount is null || bankAccount.Entries is null) return;

            var entryToUpdate = bankAccount.Entries.FirstOrDefault(x => x.EntryId == bankAccountEntry.EntryId);
            if (entryToUpdate is null) return;

            entryToUpdate.Update(bankAccountEntry);
        }
        private void UpdateStockAccountEntry(int id, StockAccountEntry investmentEntry)
        {
            var investmentAccount = FindAccount<StockAccount>(id);
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
        public void InitializeMock()
        {
            AddMockData();
        }
    }
}
