namespace FinanceManager.Domain.Dashboard.Dtos;

/// <summary>Inclusive timestamp range; timestamps without a time zone are interpreted as UTC.</summary>
public record TransactionLogQuery(DateTime Start, DateTime End, int PageSize = 25, TransactionLogCursor? Cursor = null);