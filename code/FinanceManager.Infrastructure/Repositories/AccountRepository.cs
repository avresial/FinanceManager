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

        public AccountRepository(IBankAccountRepository<BankAccount> bankAccountAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository)
        {
            _bankAccountAccountRepository = bankAccountAccountRepository;
            _bankAccountEntryRepository = bankAccountEntryRepository;
        }

        public Dictionary<int, Type> GetAvailableAccounts(int userId)
        {
            return _bankAccountAccountRepository.GetAvailableAccounts(userId)
                .ToDictionary(x => x.AccountId, x => typeof(BankAccount));
        }
        public int GetLastAccountId()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public T? GetAccount<T>(int userId, int accountId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
        {
            if (typeof(T) == typeof(BankAccount))
            {
                var availableAccounts = _bankAccountAccountRepository.GetAvailableAccounts(userId).Where(x => x.AccountId == accountId);

                foreach (var item in availableAccounts)
                {
                    var resultAccount = _bankAccountAccountRepository.Get(item.AccountId);
                    if (resultAccount is null) continue;

                    IEnumerable<BankAccountEntry> entries = _bankAccountEntryRepository.Get(item.AccountId, dateStart, dateEnd).ToList();

                    DateTime? olderThanLoadedEntryDate = null;
                    var olderEntry = _bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);
                    if (olderEntry is not null) olderThanLoadedEntryDate = olderEntry.PostingDate;

                    DateTime? youngerThanLoadedEntryDate = null;
                    var youngerEntry = _bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);
                    if (youngerEntry is not null) youngerThanLoadedEntryDate = youngerEntry.PostingDate;

                    var newResultAccount = new BankAccount(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                        resultAccount.AccountType, olderThanLoadedEntryDate, youngerThanLoadedEntryDate);

                    resultAccount.Add(entries, false);
                    if (newResultAccount is T resultElement)
                        return resultElement;
                }
            }

            return null;
        }
        public T? GetAccount<T>(int userId, int id) where T : BasicAccountInformation
        {
            return GetAccount<T>(userId, id, DateTime.UtcNow, DateTime.UtcNow);
        }
        public IEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
        {
            List<T> result = new();
            if (typeof(T) == typeof(BankAccount))
            {
                var availableAccounts = _bankAccountAccountRepository.GetAvailableAccounts(userId);

                foreach (var item in availableAccounts)
                {
                    var resultAccount = _bankAccountAccountRepository.Get(item.AccountId);
                    if (resultAccount is null) continue;

                    IEnumerable<BankAccountEntry> entries = _bankAccountEntryRepository.Get(item.AccountId, dateStart, dateEnd).ToList();

                    DateTime? olderThanLoadedEntryDate = null;
                    var olderEntry = _bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);
                    if (olderEntry is not null) olderThanLoadedEntryDate = olderEntry.PostingDate;

                    DateTime? youngerThanLoadedEntryDate = null;
                    var youngerEntry = _bankAccountEntryRepository.GetNextOlder(item.AccountId, dateStart);
                    if (youngerEntry is not null) youngerThanLoadedEntryDate = youngerEntry.PostingDate;

                    var newResultAccount = new BankAccount(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                        resultAccount.AccountType, olderThanLoadedEntryDate, youngerThanLoadedEntryDate);

                    resultAccount.Add(entries, false);
                    if (newResultAccount is T resultElement)
                        result.Add(resultElement);
                }
            }

            return result;
        }

        public void AddAccount<T>(T account) where T : BasicAccountInformation
        {
            if (account is BankAccount bankAccount)
            {
                _bankAccountAccountRepository.Add(bankAccount.UserId, bankAccount.AccountId, bankAccount.Name, bankAccount.AccountType);

                if (bankAccount is not null && bankAccount.Entries is not null)
                    foreach (var entry in bankAccount.Entries)
                        _bankAccountEntryRepository.Add(entry);
                return;
            }

            throw new NotSupportedException($"Account type {account.GetType()} is not supported.");
        }
        public void AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
            where AccountType : BasicAccountInformation
            where EntryType : FinancialEntryBase
        {
            throw new NotImplementedException();
        }
        public void UpdateAccount<T>(T account) where T : BasicAccountInformation
        {
            throw new NotImplementedException();
        }
        public void RemoveAccount(int id)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
        private object? FindAccount(int id)
        {
            throw new NotImplementedException();
        }
        private T? FindAccount<T>(int id) where T : BasicAccountInformation
        {
            throw new NotImplementedException();
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
    }
}
