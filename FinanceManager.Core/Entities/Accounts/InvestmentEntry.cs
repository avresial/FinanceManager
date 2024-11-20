using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialEntryBase
    {
        public int Id { get; set; }
        public DateTime PostingDate { get; internal set; }
        public decimal Value { get; set; }
        public decimal ValueChange { get; internal set; }

        public FinancialEntryBase(DateTime postingDate, decimal value, decimal valueChange)
        {
            PostingDate = postingDate;
            Value = value;
            ValueChange = valueChange;
        }

    }
    public class InvestmentEntry : FinancialEntryBase
    {
        public string Ticker { get; set; }
        public InvestmentType InvestmentType { get; set; }
        public InvestmentEntry(DateTime postingDate, decimal value, decimal valueChange, string ticker, InvestmentType investmentType) : base(postingDate, value, valueChange)
        {
            Ticker = ticker;
            InvestmentType = investmentType;
        }

    }

    public class FixedAssetEntry : FinancialEntryBase
    {
        public string Name { get; set; }
        public string Currency { get; set; }

        public FixedAssetEntry(DateTime postingDate, decimal value, decimal valueChange, string name, string currency) : base(postingDate, value, valueChange)
        {
            Name = name;
            Currency = currency;
        }

    }
    public class BankAccountEntry : FinancialEntryBase
    {
        public string Description { get; set; }
        public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

        public BankAccountEntry(DateTime postingDate, decimal value, decimal valueChange) : base(postingDate, value, valueChange)
        {
        }
    }
}
