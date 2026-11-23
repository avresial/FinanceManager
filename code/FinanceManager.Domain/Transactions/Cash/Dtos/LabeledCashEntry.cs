namespace FinanceManager.Domain.Transactions.Cash.Dtos;

/// <summary>One of the owner's cash entries reduced to what money-flow reports need: currency, date, signed change and the ids of its labels.</summary>
public sealed record LabeledCashEntry(int CurrencyId, DateTime PostingDate, decimal ValueChange, IReadOnlyList<int> LabelIds);