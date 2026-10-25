using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Components.Features.Dashboard.Models;

public static class DistributionSlices
{
    public const int MaxNamedSlices = 5;
    public const string OtherName = "Other";

    /// <summary>
    /// Sorts items by value and keeps the largest <see cref="MaxNamedSlices"/> as named slices. The rest are
    /// combined into one "Other" slice, unless only a single item would be left over: an "Other" that hides
    /// one name carries no benefit, so that item keeps its own slice.
    /// </summary>
    public static List<DistributionSlice> Build(IEnumerable<NameValueResult> items)
    {
        var sorted = items.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ToList();
        var total = sorted.Sum(x => x.Value);
        if (total <= 0) return [];

        var groupOther = sorted.Count > MaxNamedSlices + 1;
        var named = groupOther ? sorted.Take(MaxNamedSlices).ToList() : sorted;
        var colors = ColorsProvider.GetDistributionColors();

        List<DistributionSlice> slices =
        [
            .. named.Select((item, index) => new DistributionSlice(
                item.Name, item.Value, Percent(item.Value, total), colors[index % colors.Count], item.AccountId, []))
        ];

        if (groupOther)
        {
            var rest = sorted.Skip(MaxNamedSlices).ToList();
            var otherValue = rest.Sum(x => x.Value);
            slices.Add(new DistributionSlice(OtherName, otherValue, Percent(otherValue, total), ColorsProvider.OtherColor, null, rest));
        }

        return slices;
    }

    private static decimal Percent(decimal value, decimal total) => Math.Round(value / total * 100m, 1);
}