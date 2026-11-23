namespace FinanceManager.Domain.Transactions.Shared.Dtos;

public record OwnerHistoryQuery(DateTime? Start, DateTime? End, int Take, OwnerHistoryBoundary? Boundary = null,
    IReadOnlyList<string>? Currencies = null, string? Search = null, bool IncludeDetails = true)
{
    /// <summary>The requested currencies trimmed, upper-cased and de-duplicated, so readers match them against upper-cased codes.</summary>
    public IReadOnlyList<string> NormalizedCurrencies() =>
        Currencies is null ? [] : [.. Currencies.Select(code => code.Trim().ToUpperInvariant()).Where(code => code.Length > 0).Distinct()];
}