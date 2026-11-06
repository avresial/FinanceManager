namespace FinanceManager.Application.Labels.RecurringTransactions;

/// <summary>One transaction considered for recurrence detection; <see cref="Amount"/> is always positive.</summary>
internal sealed record RecurringEntry(string Merchant, DateTime Date, decimal Amount, int AccountId, long EntryId);