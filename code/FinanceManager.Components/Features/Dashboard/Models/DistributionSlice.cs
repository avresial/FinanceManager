using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>One donut slice and legend row. <see cref="Members"/> is non-empty only for the grouped "Other" slice.</summary>
public sealed record DistributionSlice(
    string Name,
    decimal Value,
    decimal Percent,
    string Color,
    int? AccountId,
    IReadOnlyList<NameValueResult> Members)
{
    public bool IsOther => Members.Count > 0;
}