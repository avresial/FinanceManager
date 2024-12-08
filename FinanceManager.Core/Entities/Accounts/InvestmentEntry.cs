using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialEntryBase
    {
        public int Id { get; internal set; }
        public DateTime PostingDate { get; internal set; }
        public decimal Value { get; set; }
        public decimal ValueChange { get; internal set; }

        public FinancialEntryBase(int id, DateTime postingDate, decimal value, decimal valueChange)
        {
            Id = id;
            PostingDate = postingDate;
            Value = value;
            ValueChange = valueChange;
        }

        public virtual void Update(FinancialEntryBase financialEntryBase)
        {
            PostingDate = financialEntryBase.PostingDate;

            var valueChangeChange = financialEntryBase.ValueChange - ValueChange;
            Value += valueChangeChange;

            ValueChange = financialEntryBase.ValueChange;
        }
    }

    public class InvestmentEntry : FinancialEntryBase
    {
        public string Ticker { get; set; }
        public InvestmentType InvestmentType { get; set; }
        public InvestmentEntry(int id, DateTime postingDate, decimal value, decimal valueChange, string ticker, InvestmentType investmentType)
            : base(id, postingDate, value, valueChange)
        {
            Ticker = ticker;
            InvestmentType = investmentType;
        }
        public void Update(InvestmentEntry entry)
        {
            PostingDate = entry.PostingDate;

            var valueChangeChange = entry.ValueChange - ValueChange;
            Value += valueChangeChange;

            ValueChange = entry.ValueChange;
            Ticker = entry.Ticker;
            InvestmentType = entry.InvestmentType;
        }
    }
    public class FixedAssetEntry : FinancialEntryBase
    {
        public string Name { get; set; }
        public string Currency { get; set; }

        public FixedAssetEntry(int id, DateTime postingDate, decimal value, decimal valueChange, string name, string currency)
            : base(id, postingDate, value, valueChange)
        {
            Name = name;
            Currency = currency;
        }

    }
    public class BankAccountEntry : FinancialEntryBase
    {
        public string Description { get; set; }
        public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

        public BankAccountEntry(int id, DateTime postingDate, decimal value, decimal valueChange) : base(id, postingDate, value, valueChange)
        {
        }

        public void Update(BankAccountEntry entry)
        {
            PostingDate = entry.PostingDate;

            var valueChangeChange = entry.ValueChange - ValueChange;
            Value += valueChangeChange;

            ValueChange = entry.ValueChange;
            Description = entry.Description;
            ExpenseType = entry.ExpenseType;
        }
    }
}
