using System.Globalization;

namespace FinanceManager.Components.Shared.Helpers;

/// <summary>
/// The single place that turns a money amount into display text, so every card, legend, list and tooltip
/// shows the same shape: grouped thousands, two decimals and the ISO currency code (<c>27,771.00 PLN</c>).
/// </summary>
/// <remarks>
/// The culture is fixed (invariant) on purpose. Output must not change with the browser or OS locale,
/// otherwise the same value reads differently from one machine, card or test run to another.
/// </remarks>
public static class MoneyFormatter
{
    private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    /// <summary>Formats <paramref name="amount"/> with an optional currency suffix, e.g. <c>-1,000.00 PLN</c>.</summary>
    public static string Format(decimal amount, string? currency = null) =>
        WithCurrency(FormatNumber(amount), currency);

    /// <summary>
    /// Like <see cref="Format"/> but with an explicit <c>+</c> for positive amounts, for values that show direction
    /// (cash flow, gain / loss). Zero carries no sign.
    /// </summary>
    public static string FormatSigned(decimal amount, string? currency = null)
    {
        var rounded = Round(amount);
        var number = FormatNumber(rounded);
        return WithCurrency(rounded > 0m ? "+" + number : number, currency);
    }

    /// <summary>Formats only the number part (<c>27,771.00</c>), for places where the currency is shown elsewhere.</summary>
    public static string FormatNumber(decimal amount) => Round(amount).ToString("N2", _culture);

    // Rounding first avoids "-0.00" for tiny negatives.
    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static string WithCurrency(string number, string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? number : $"{number} {currency}";
}