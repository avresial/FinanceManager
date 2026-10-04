using MudBlazor;

namespace FinanceManager.Components.Shared.Helpers;

/// <summary>Presentation rules shared by transaction list rows: when to show a time and which icon stands in for a category.</summary>
public static class TransactionRowPresentation
{
    // Ordered: first matching keyword wins. Labels are free text, so matching is keyword based
    // and anything unknown falls back to the direction of the money flow.
    private static readonly (string[] Keywords, string Icon)[] LabelIcons =
    [
        (["salary", "wage", "payroll", "paycheck", "income"], Icons.Material.Filled.Payments),
        (["grocer", "food", "supermarket", "restaurant", "cafe", "dining"], Icons.Material.Filled.ShoppingCart),
        (["rent", "mortgage", "housing", "home"], Icons.Material.Filled.Home),
        (["invest", "stock", "bond", "etf", "broker", "saving"], Icons.Material.Filled.TrendingUp),
        (["transport", "fuel", "car", "taxi", "travel", "train"], Icons.Material.Filled.DirectionsCar),
        (["utilit", "electric", "water", "gas", "internet", "phone"], Icons.Material.Filled.Bolt),
        (["subscription", "netflix", "spotify", "streaming"], Icons.Material.Filled.Subscriptions),
        (["health", "pharmacy", "doctor", "medical"], Icons.Material.Filled.LocalHospital),
        (["transfer"], Icons.Material.Filled.SwapHoriz),
        (["tax", "insurance", "fee"], Icons.Material.Filled.Receipt)
    ];

    public static string IconFor(string? labelName, decimal valueChange)
    {
        if (!string.IsNullOrWhiteSpace(labelName))
        {
            foreach (var (keywords, icon) in LabelIcons)
            {
                if (keywords.Any(keyword => labelName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    return icon;
            }
        }

        return valueChange >= 0 ? Icons.Material.Filled.ArrowDownward : Icons.Material.Filled.ArrowUpward;
    }

    /// <summary>
    /// A time carries information only when it is not midnight (a date-only placeholder) and
    /// the rows it is shown beside do not all share the same stamp (a seeded or imported default).
    /// </summary>
    public static bool ShouldShowTime(DateTime localPostingDate, bool siblingsShareSameTime) =>
        localPostingDate.TimeOfDay != TimeSpan.Zero && !siblingsShareSameTime;

    /// <summary>True when more than one row exists and all of them have the identical time of day.</summary>
    public static bool AllShareSameTime(IEnumerable<DateTime> localPostingDates)
    {
        var times = localPostingDates.Select(date => date.TimeOfDay).ToList();
        return times.Count > 1 && times.Distinct().Count() == 1;
    }
}