namespace FinanceManager.Domain.Dashboard.Dtos;

public record TransactionLogEntryDto(TransactionKind Kind, long EntryId, DateTime Date,
    decimal ValueChange, string Description, string? CurrencyOrUnit = null);