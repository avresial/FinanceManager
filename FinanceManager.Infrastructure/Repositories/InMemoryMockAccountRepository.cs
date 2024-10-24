﻿using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;
using System.ComponentModel.Design;

namespace FinanceManager.Infrastructure.Repositories
{
    public class InMemoryMockAccountRepository : IFinancalAccountRepository
    {
        private readonly Random random = new Random();
        private readonly ServiceContainer _bankAccounts = new ServiceContainer();
        Dictionary<string, Type> nameTypeDictionary = new Dictionary<string, Type>();

        public InMemoryMockAccountRepository()
        {
            AddMockData();
        }

        public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase
        {
            var accountsOfType = _bankAccounts.GetService(typeof(List<T>)) as List<T>;
            if (accountsOfType is null)
                return Enumerable.Empty<T>();

            List<T> result = new List<T>();
            foreach (var account in accountsOfType)
            {
                if (account is null) continue;

                string name = account.Name;
                var newAccount = GetAccount<T>(name, dateStart, dateEnd);
                if (newAccount is null) continue;

                result.Add(newAccount);
            }
            return result;
        }

        public T? GetAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase
        {
            List<T> accountsOfType = _bankAccounts.GetService(typeof(List<T>)) as List<T>;
            if (accountsOfType is null) return default;

            var firstAccount = accountsOfType.FirstOrDefault(x => x.Name == name);
            if (firstAccount is null) return default;

            if (typeof(T) == typeof(BankAccount))
            {
                BankAccount databaseBankAccount = firstAccount as BankAccount;

                BankAccount bankAccount = new BankAccount(name, databaseBankAccount.Entries, databaseBankAccount.AccountType);
                bankAccount.SetDates(dateStart, dateEnd);
                return bankAccount as T;
            }

            if (typeof(T) == typeof(InvestmentAccount))
            {
                InvestmentAccount databaseBankAccount = firstAccount as InvestmentAccount;

                InvestmentAccount bankAccount = new InvestmentAccount(name, databaseBankAccount.Entries);
                bankAccount.SetDates(dateStart, dateEnd);
                return bankAccount as T;
            }

            return default;
        }
        public List<T>? GetEntries<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialEntryBase
        {


            return null;
        }

        public void AddFinancialAccount<T>(T bankAccount) where T : FinancialAccountBase
        {
            if (_bankAccounts is null) return;

            var accountsOfType = (List<T>)_bankAccounts.GetService(typeof(List<T>));
            if (accountsOfType is null)
                _bankAccounts.AddService(typeof(List<T>), new List<T>() { bankAccount });
            else
                accountsOfType.Add(bankAccount);

            nameTypeDictionary.Add(bankAccount.Name, typeof(T));
        }


        public void AddFinancialAccount<AccountType, EntryType>(string name, List<EntryType> data) where AccountType : FinancialAccountBase where EntryType : FinancialEntryBase
        {
            //var mainBankAccount = _bankAccounts.FirstOrDefault(x => x.Name == name);

            //if (mainBankAccount is null) return;
            //foreach (var item in data)
            //	AddFinancialAccount(name, item.BalanceChange, item.Description, item.ExpenseType, item.PostingDate);
        }

        public bool Exists(string name)
        {
            return nameTypeDictionary.ContainsKey(name);
        }

        private T? FindAccount<T>(string name) where T : FinancialAccountBase
        {
            List<T> accountsOfType = _bankAccounts.GetService(typeof(List<T>)) as List<T>;
            if (accountsOfType is null) return default;

            var firstAccount = accountsOfType.FirstOrDefault(x => x.Name == name);
            if (firstAccount is null) return default;

            return firstAccount;
        }
        private void AddMockData()
        {
            AddBankAccount(DateTime.UtcNow.AddMonths(-8), 100, "Main", AccountType.Cash);
            //		AddBankAccount(DateTime.UtcNow.AddMonths(-8), 10, "Cash", AccountType.Cash);

            //AddBankAccount(DateTime.UtcNow.AddMonths(-8), 10, "Bonds", AccountType.Bond);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-12), 10, "Nvidia", AccountType.Stock);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-12), 10, "Intel", AccountType.Stock);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-12), 10, "S&P 500", AccountType.Stock);
            //AddBankAccount(DateTime.UtcNow.AddMonths(-2), 10, "PPK", AccountType.Stock);

            AddBankAccount(DateTime.UtcNow.AddMonths(-3), 10000, "Apartment 1", AccountType.RealEstate);
            AddLoanAccount(DateTime.UtcNow.AddMonths(-2), -300 * 62, "Loan 1");

            AddStockAccount(DateTime.UtcNow.AddDays(-10), 10, "Wallet 1", new List<(string, InvestmentType)>()
            {
                ("S&P 500", InvestmentType.Stock)
                ,( "Orlen", InvestmentType.Stock), 
                //("Nvidia", InvestmentType.Stock),( "Intel", InvestmentType.Stock), ("Bonds" , InvestmentType.Bond)
            });

            AddStockAccount(DateTime.UtcNow.AddDays(-10), 10, "Wallet 2", new List<(string, InvestmentType)>()
            {
                ("Nvidia", InvestmentType.Stock),( "Intel", InvestmentType.Stock), ("Bond 1" , InvestmentType.Bond)
            });

            AddStockAccount(DateTime.UtcNow.AddMonths(-1), 10, "Wallet 3", new List<(string, InvestmentType)>()
            {
                ("Bond 2", InvestmentType.Bond),( "Bond 3", InvestmentType.Bond), ("Bond 4" , InvestmentType.Bond)
            });
        }

        private void AddStockAccount(DateTime startDay, decimal startingBalance, string accountName, List<(string, InvestmentType)> tickers)
        {
            AddFinancialAccount(new InvestmentAccount(accountName, DateTime.UtcNow, DateTime.UtcNow));
            int tickerIndex = random.Next(tickers.Count);
            AddStockAccountEntry(accountName, tickers[tickerIndex].Item1, tickers[tickerIndex].Item2, startingBalance, startDay);

            Console.WriteLine($"WARNING - now {DateTime.UtcNow.Date}");

            while (startDay.Date <= DateTime.UtcNow.Date)
            {
                tickerIndex = random.Next(tickers.Count);
                decimal balanceChange = (decimal)(random.Next(-150, 200) + Math.Round(random.NextDouble(), 2));


                AddStockAccountEntry(accountName, tickers[tickerIndex].Item1, tickers[tickerIndex].Item2, balanceChange, startDay);
                startDay = startDay.AddDays(1);
            }
        }
        private void AddStockAccountEntry(string name, string ticker, InvestmentType investmentType, decimal balanceChange, DateTime? postingDate = null)
        {
            var bankAccount = FindAccount<InvestmentAccount>(name);
            if (bankAccount is null) return;

            decimal balance = balanceChange;

            if (bankAccount.Entries is not null && bankAccount.Entries.Any(x => x.Ticker == ticker))
            {
                var lastBalance = bankAccount.Entries.First(x => x.Ticker == ticker).Value;
                if (balance + lastBalance < 0)
                {
                    balance = 0;
                    balanceChange = -lastBalance;
                }
                else
                {
                    balance += lastBalance;
                }
            }

            if (balanceChange == 0) return;

            InvestmentEntry bankAccountEntry = new InvestmentEntry(postingDate.HasValue ? postingDate.Value : DateTime.UtcNow, balance, balanceChange, ticker, investmentType)
            {
                Ticker = ticker,
            };


            if (bankAccount.Entries is not null)
                bankAccount.Entries.Insert(0, bankAccountEntry);
        }

        private void AddBankAccount(DateTime startDay, decimal startingBalance, string accountName, AccountType accountType)
        {
            ExpenseType expenseType = GetRandomType();
            if (accountType == AccountType.Stock)
                expenseType = ExpenseType.Investment;

            AddFinancialAccount(new BankAccount(accountName, accountType));
            AddBankAccountEntry(accountName, startingBalance, $"Lorem ipsum {0}", expenseType, startDay);

            int index = 0;
            while (startDay.Date <= DateTime.UtcNow.Date)
            {
                decimal balanceChange = (decimal)(random.Next(-150, 200) + Math.Round(random.NextDouble(), 2));

                expenseType = GetRandomType();
                if (accountType == AccountType.Stock)
                    expenseType = ExpenseType.Investment;

                AddBankAccountEntry(accountName, balanceChange, $"Lorem ipsum {index++}", expenseType, startDay);
                startDay = startDay.AddDays(1);
            }
        }
        private void AddLoanAccount(DateTime startDay, decimal startingBalance, string accountName)
        {
            var creditDays = (DateTime.UtcNow - startDay).TotalDays;

            AddFinancialAccount(new BankAccount(accountName, AccountType.Loan));

            AddBankAccountEntry(accountName, startingBalance, $"Lorem ipsum {0}", ExpenseType.DebtRepayment, startDay);

            decimal repaidAmount = 0;

            int index = 0;
            while (repaidAmount < -startingBalance && startDay.Date <= DateTime.Now.Date)
            {
                decimal balanceChange = (decimal)(random.Next(0, 300) + Math.Round(random.NextDouble(), 2));
                repaidAmount += balanceChange;
                if (repaidAmount >= -startingBalance)
                    balanceChange = repaidAmount + startingBalance;

                AddBankAccountEntry(accountName, balanceChange, $"Lorem ipsum {0}", ExpenseType.Other, startDay);

                //var mainBankAccount = FindAccount<BankAccount>("Main");
                //if (mainBankAccount is not null)
                //    AddBankAccountEntry("Main", -balanceChange, $"Loan repainment {0}", ExpenseType.DebtRepayment, startDay);
                startDay = startDay.AddDays(1);
            }
        }
        private void AddBankAccountEntry(string name, decimal balanceChange, string senderName, ExpenseType expenseType, DateTime? postingDate = null)
        {
            var bankAccount = FindAccount<BankAccount>(name);
            if (bankAccount is null) return;

            decimal balance = balanceChange;

            if (bankAccount.Entries is not null && bankAccount.Entries.Any())
                balance += bankAccount.Entries.Last().Value;

            BankAccountEntry bankAccountEntry = new BankAccountEntry(postingDate.HasValue ? postingDate.Value : DateTime.UtcNow, balance, balanceChange)
            {
                Description = senderName,
                ExpenseType = expenseType,
            };

            AddFinancialAccount<BankAccount, BankAccountEntry>(name, new List<BankAccountEntry>() { bankAccountEntry });

            if (bankAccount.Entries is not null)
                bankAccount.Entries.Insert(0, bankAccountEntry);
        }


        private ExpenseType GetRandomType()
        {
            Array values = Enum.GetValues(typeof(ExpenseType));
            return (ExpenseType)values.GetValue(random.Next(values.Length));
        }

        private decimal GetRandomBalanceChange()
        {
            return (decimal)(random.Next(-100, 100) + Math.Round(random.NextDouble(), 2));
        }

        public Dictionary<string, Type> GetAvailableAccounts()
        {
            return nameTypeDictionary;
        }
    }
}
