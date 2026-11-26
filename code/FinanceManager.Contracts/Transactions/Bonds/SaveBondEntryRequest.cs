namespace FinanceManager.Contracts.Transactions.Bonds;

/// <summary>
/// Request to add a bond entry to, or change one in, the caller's pool. <paramref name="ValueChange"/> is the signed amount.
/// Omitted <paramref name="LabelIds"/> attaches no labels on create and leaves them unchanged on update; an empty list clears them.
/// </summary>
public record SaveBondEntryRequest(
    int BondDetailsId,
    DateTime PostingDate,
    decimal ValueChange,
    string? Description,
    IReadOnlyList<int>? LabelIds = null);