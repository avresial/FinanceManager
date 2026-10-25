namespace FinanceManager.Domain.Shared.Charting;

public static class ColorsProvider
{
    public static List<string> GetColors() =>
    [
            "#FFAB00",
            "#E69649",
            "#476A5D",
            "#4D4E8F",
            "#60B8AF",
    ];

    // Distribution slices are sorted by value, so neighbours must differ clearly in hue and stay
    // readable on both the light and the dark theme. Based on the Tableau 10 palette.
    public static List<string> GetDistributionColors() =>
    [
            "#4E79A7",
            "#F28E2B",
            "#59A14F",
            "#E15759",
            "#B07AA1",
            "#76B7B2",
            "#EDC948",
            "#FF9DA7",
    ];

    // Neutral colour for the grouped "Other" slice so it never reads as one of the named items.
    public const string OtherColor = "#9E9E9E";
}