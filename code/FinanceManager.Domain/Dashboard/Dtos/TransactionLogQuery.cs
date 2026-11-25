namespace FinanceManager.Domain.Dashboard.Dtos;

public record TransactionLogQuery(
    DateTime Start, DateTime End, int PageSize = 25, TransactionLogCursor? Cursor = null,
    IReadOnlyList<TransactionKind>? Kinds = null, IReadOnlyList<string>? Currencies = null, string? Search = null)
{
    public static DateTime AsUtc(DateTime date) => date.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
        : date.ToUniversalTime();
}