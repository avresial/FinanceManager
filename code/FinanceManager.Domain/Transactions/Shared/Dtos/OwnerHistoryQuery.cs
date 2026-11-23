namespace FinanceManager.Domain.Transactions.Shared.Dtos;

public record OwnerHistoryQuery(DateTime? Start, DateTime? End, int Take, OwnerHistoryBoundary? Boundary = null,
    IReadOnlyList<string>? Currencies = null, string? Search = null, bool IncludeDetails = true);