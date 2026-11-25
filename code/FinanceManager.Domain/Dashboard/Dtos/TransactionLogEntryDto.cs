namespace FinanceManager.Domain.Dashboard.Dtos;

/// <param name="Subtitle">What tells rows of one kind apart: the first label of a cash entry, the bond name, or the ticker.</param>
public record TransactionLogEntryDto(TransactionKind Kind, long EntryId, DateTime Date,
    decimal ValueChange, string Description, string? CurrencyOrUnit = null, string? Subtitle = null);