namespace FinanceManager.Components.Shared.Helpers;

/// <summary>Theme-aware brand amber for balance charts; other series retain their semantic colours.</summary>
public static class BalanceChartColors
{
    public const string DarkAccent = "#FFAB00";

    // The developer approved this exact midpoint over the original 3:1 target in #1046.
    // It reads at 2.77:1 on white; it is an improvement, not a claim of 3:1 compliance.
    public const string LightAccent = "#D38D00";

    public static string Accent(bool isDarkMode) => isDarkMode ? DarkAccent : LightAccent;

    public static string ForTheme(string color, bool isDarkMode) =>
        color.Equals(DarkAccent, StringComparison.OrdinalIgnoreCase) ? Accent(isDarkMode) : color;
}