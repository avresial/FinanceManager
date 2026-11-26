using System.Text.Json.Serialization;

namespace FinanceManager.Contracts.Transactions.Cash;

/// <summary>
/// Request to add a cash entry to the caller's pool of one currency. The owner comes from the authenticated principal;
/// there is no account. <paramref name="ValueChange"/> is the signed amount. <paramref name="LabelIds"/> attaches labels;
/// omitted or empty attaches none. The currency must be sent explicitly: an omitted one is rejected instead of selecting
/// the default currency, whose id is 0.
/// </summary>
public record CreateCashEntryRequest(
    [property: JsonRequired] int CurrencyId,
    DateTime PostingDate,
    decimal ValueChange,
    string Description,
    string? ContractorDetails,
    IReadOnlyList<int>? LabelIds = null);