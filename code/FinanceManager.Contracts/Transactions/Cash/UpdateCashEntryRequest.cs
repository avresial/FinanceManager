namespace FinanceManager.Contracts.Transactions.Cash;

/// <summary>
/// Request to change a cash entry of the caller. The entry keeps its currency. Omitted <paramref name="LabelIds"/> leaves the
/// labels unchanged and an empty list clears them.
/// </summary>
public record UpdateCashEntryRequest(
    DateTime PostingDate,
    decimal ValueChange,
    string Description,
    string? ContractorDetails,
    IReadOnlyList<int>? LabelIds = null);