using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// One donut slice and legend row. <see cref="Members"/> is non-empty only for the grouped "Other" slice. <see cref="CurrencyCode"/> is the currency
/// the value is in when it was not converted to the card's currency; <see cref="Kind"/> and <see cref="SourceName"/> are the holding's class and its name without that class.
/// </summary>
public sealed record DistributionSlice(
    string Name,
    decimal Value,
    decimal Percent,
    string Color,
    int? AccountId,
    IReadOnlyList<NameValueResult> Members,
    string? CurrencyCode = null,
    string? Kind = null,
    string? SourceName = null)
{
    public bool IsOther => Members.Count > 0;
}