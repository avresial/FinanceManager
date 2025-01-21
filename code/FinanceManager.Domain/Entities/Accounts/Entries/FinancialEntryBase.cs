﻿namespace FinanceManager.Domain.Entities.Accounts
{
    public class FinancialEntryBase
    {
        public int EntryId { get; internal set; }
        public int AccountId { get; internal set; }
        public DateTime PostingDate { get; internal set; }
        public decimal Value { get; set; }
        public decimal ValueChange { get; internal set; }

        public FinancialEntryBase(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
        {
            AccountId = accountId;
            EntryId = entryId;
            PostingDate = postingDate;
            Value = value;
            ValueChange = valueChange;
        }

        public virtual void Update(FinancialEntryBase financialEntryBase)
        {
            AccountId = financialEntryBase.AccountId;
            if (EntryId != financialEntryBase.EntryId) throw new Exception("Entry id can not be changed.");

            PostingDate = financialEntryBase.PostingDate;

            var valueChangeChange = financialEntryBase.ValueChange - ValueChange;
            Value += valueChangeChange;

            ValueChange = financialEntryBase.ValueChange;
        }
    }
}